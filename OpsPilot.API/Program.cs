using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using OpsPilot.Application;
using OpsPilot.Application.Abstractions;
using OpsPilot.API.Health;
using OpsPilot.API.Hubs;
using OpsPilot.API.Middleware;
using OpsPilot.API.RateLimiting;
using OpsPilot.API.Realtime;
using OpsPilot.API.Scheduling;
using OpsPilot.Infrastructure;
using Serilog;
using System.Text.Json.Serialization;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        // Publishes the Bearer scheme so Swagger UI renders an Authorize button. Without it
        // there is no way to attach a token from the UI and every protected endpoint looks
        // unreachable.
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "Paste the access token below, prefixed with 'Bearer '.",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });

        // Ingestion authenticates with a key, not a token: /api/v1/events reads X-API-Key and
        // no other endpoint does. Publishing a scheme for the header is what lets Swagger UI
        // send one at all — without it the ingestion button can only ever return 401.
        options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
        {
            Name = "X-API-Key",
            Description = "An ingestion key issued by POST /api/v1/api-keys, sent as-is. " +
                          "A key that was lost can be read back from GET /api/v1/api-keys.",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey
        });

        // The requirement is bound to the document so the scheme reference resolves against the
        // components section SwaggerGen emits for the definition above.
        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
        });

        // A second entry, not a second condition: OpenAPI lists security requirements as
        // alternatives, so an operation may be called with either credential and no endpoint
        // demands both.
        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("ApiKey", document)] = new List<string>()
        });

        // The document is the versioned API surface only. That also keeps non-API endpoints such
        // as /health out of it, so every operation shown is one a client is meant to call.
        options.DocInclusionPredicate((_, api) =>
            api.RelativePath?.StartsWith("api/v1/", StringComparison.OrdinalIgnoreCase) == true);
    });

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Add services to the container.
    builder.Services.AddControllers()
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();
    builder.Services.AddProblemDetails();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Hangfire as the scheduler for the recurring passes (plan, "BackgroundJobs"). Registered
    // after AddInfrastructure so the workers it schedules already exist, and a no-op unless
    // Hangfire:Enabled is set, in which case the workers keep their own timers.
    builder.Services.AddScheduledJobs(builder.Configuration);

    // The plan's ingestion limit (429 past a fixed window per key) is an endpoint policy rather
    // than a global one; see IngestionRateLimitingExtensions for where it sits in the pipeline.
    builder.Services.AddIngestionRateLimiting(builder.Configuration);

    // Live updates (plan, UC-15 and FR-20): the hub itself lives in the API layer, while the
    // notifier is what handlers and repositories talk to, so neither has to know SignalR exists.
    builder.Services.AddSignalR();
    builder.Services.AddSingleton<IIncidentNotifier, IncidentsHubNotifier>();

    builder.Services.AddScoped<DatabaseHealthCheck>();
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    app.UseSerilogRequestLogging();

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseSwagger();
        app.UseSwaggerUI();

        // Serves the development console for the live incident channel at /signalr-test.
        // It sits inside the Development branch on purpose: it is an operator's bench, not part
        // of the product, and a deployed environment has no reason to carry it. Serving it from
        // the application's own origin is also what keeps the page free of CORS — the socket it
        // opens and the sign-in call it makes are same-origin requests.
        app.UseStaticFiles();

        // The static middleware only answers the file's own path; this gives the page the short
        // address it is documented under. A redirect rather than a rewrite so the browser's URL
        // matches the file it ends up showing.
        app.MapGet("/signalr-test", () => Results.Redirect("/signalr-test.html"));
    }

    app.UseHttpsRedirection();

    app.UseAuthentication();

    // Immediately after authentication: the throttle partitions on the API key claim a request
    // has proved it carries, and that claim exists the moment the scheme has run. Sitting before
    // the tenant check also means a flood of unknown keys lands in the one shared bucket rather
    // than being dropped before it is counted at all.
    app.UseRateLimiter();

    // Must sit between authentication and authorization: the tenant claim only exists once the
    // bearer token has been validated, and the tenant must be resolved before any handler runs.
    app.UseMiddleware<TenantResolutionMiddleware>();

    app.UseAuthorization();

    app.MapControllers();

    // Registers the recurring jobs and mounts the dashboard. A no-op when Hangfire is off, so
    // this is always safe to call. It sits here rather than earlier because a recurring job is
    // written to storage and needs the built container.
    app.UseScheduledJobs();

    // The hub sits behind the same authentication and tenant resolution as the controllers,
    // which is why it is mapped after both -- a connection is a request like any other until
    // the upgrade completes.
    app.MapHub<IncidentsHub>("/hubs/incidents");

    // Liveness never touches the database; readiness only evaluates checks tagged "ready".
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready");
    app.MapHealthChecks("/health");

    app.Run();
    return 0;
}
catch (HostAbortedException)
{
    // Thrown by the host factory resolver when design-time tooling (dotnet ef) builds the
    // host and then aborts it. It is expected there and must not be logged as a crash.
    return 0;
}
catch (Exception exception)
{
    Log.Fatal(exception, "OpsPilot.API terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program
{
}
