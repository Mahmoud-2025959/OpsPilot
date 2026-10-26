using System.Security.Claims;
using System.Text;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Settings;
using OpsPilot.Contracts.Auth;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Identity;
using OpsPilot.Infrastructure.BackgroundJobs;
using OpsPilot.Infrastructure.Identity;
using OpsPilot.Infrastructure.Integration;
using OpsPilot.Infrastructure.Persistence;
using OpsPilot.Infrastructure.Persistence.Interceptors;
using OpsPilot.Infrastructure.RabbitMQ;
using OpsPilot.Infrastructure.Security;
using OpsPilot.Infrastructure.Services;
using OpsPilot.Infrastructure.Webhooks;

namespace OpsPilot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddScoped<ICurrentTenant, TenantContext>();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        services.AddScoped<AuditingSaveChangesInterceptor>();
        services.AddScoped<AppendOnlySaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            options.UseSqlServer(connectionString);

            // The tenant query filter bakes the ambient tenant id into the model, so EF's
            // default cache key (context type alone) would serve tenant A's model to tenant B.
            options.ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>();

            // Audit stamping and the append-only rule are enforced by interceptors rather
            // than in SaveChanges, so they apply to every save path the caller might use.
            options.AddInterceptors(
                provider.GetRequiredService<AuditingSaveChangesInterceptor>(),
                provider.GetRequiredService<AppendOnlySaveChangesInterceptor>());
        });

        services.AddIdentityCore<User>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<Role>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<IPostmortemRepository, PostmortemRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IServiceRepository, ServiceRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ISlaPolicyRepository, SlaPolicyRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
        services.AddScoped<ISlaViolationRepository, SlaViolationRepository>();
        services.AddScoped<ITenantDirectory, TenantDirectory>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IInboxRepository, InboxRepository>();
        services.AddScoped<IWebhookRepository, WebhookRepository>();
        services.AddScoped<IntegrationEventProcessor>();

        // The outbox publishing step is a broker when one is configured, and a logging stand-in
        // when it is not (plan, UC-06 step 7). That is the one line the abstraction was built
        // for: nothing else in the pipeline names a transport. The connection is a singleton
        // because an AMQP connection is opened once and shared; the publisher is scoped so it
        // lines up with the unit of work that resolves it.
        var rabbitSection = configuration.GetSection(RabbitMqOptions.SectionName);

        services.Configure<RabbitMqOptions>(rabbitSection);

        var rabbitOptions = rabbitSection.Get<RabbitMqOptions>() ?? new RabbitMqOptions();

        if (rabbitOptions.Enabled || rabbitOptions.ConsumerEnabled)
        {
            services.AddSingleton<RabbitMqConnection>();
        }

        if (rabbitOptions.Enabled)
        {
            services.AddScoped<IOutboxPublisher, RabbitMqOutboxPublisher>();
        }
        else
        {
            services.AddScoped<IOutboxPublisher, LoggingOutboxPublisher>();
        }

        // The other end of the outbox. Off by default, like the publisher: a fresh clone starts
        // without requiring a broker, and a company that never publishes an event never needs
        // one.
        if (rabbitOptions.ConsumerEnabled)
        {
            services.AddHostedService<RabbitMqIntegrationConsumer>();
        }

        services.AddScoped<IEmailSender, MailKitEmailSender>();
        services.AddScoped<ITenantProvisioner, TenantProvisioner>();

        // The catalogue is seeded when a company is created, which leaves every company that
        // already exists without a permission a later build introduced. This catches them up
        // once at startup. Registered as a singleton as well as a hosted service so a test can
        // resolve it and drive a pass itself.
        var permissionSyncSection = configuration.GetSection(PermissionSyncOptions.SectionName);
        var permissionSyncEnabled = permissionSyncSection.Get<PermissionSyncOptions>()?.Enabled ?? true;

        services.Configure<PermissionSyncOptions>(permissionSyncSection);
        services.AddSingleton<PermissionCatalogSynchronizer>();

        if (permissionSyncEnabled)
        {
            services.AddHostedService(
                provider => provider.GetRequiredService<PermissionCatalogSynchronizer>());
        }

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();

        // The stored copy of an ingestion key. The configuration is validated here rather than
        // when the protector is first resolved, so an installation without a key says so at
        // startup instead of discovering it on the first key it is asked to store.
        var apiKeyEncryptionSection = configuration.GetSection(ApiKeyEncryptionOptions.SectionName);

        services.Configure<ApiKeyEncryptionOptions>(apiKeyEncryptionSection);

        ApiKeyEncryptionOptions.Validate(
            apiKeyEncryptionSection.Get<ApiKeyEncryptionOptions>() ?? new ApiKeyEncryptionOptions());

        services.AddSingleton<ISecretProtector, AesGcmApiKeySecretProtector>();

        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        // Validated here, at startup, for the same reason the encryption key is: a signing key
        // that is missing or too short is a misconfiguration, and the crypto stack would
        // otherwise report it much later and in far less useful words.
        JwtOptions.Validate(jwtOptions);

        services.AddAuthentication(options =>
        {
            // The default is a policy scheme, not a concrete one: it inspects the request and
            // forwards to whichever credential was presented. That matters beyond tidiness --
            // authentication has to run during UseAuthentication so that
            // TenantResolutionMiddleware, which runs immediately after, can read the tenant claim
            // the bearer token or the API key produced. A scheme picked only later, at
            // authorization time, would leave the tenant unresolved for the whole request.
            options.DefaultAuthenticateScheme = ApiKeyAuthenticationDefaults.SmartScheme;
            options.DefaultChallengeScheme = ApiKeyAuthenticationDefaults.SmartScheme;
            options.DefaultScheme = ApiKeyAuthenticationDefaults.SmartScheme;
        })
        // Applications present an X-API-Key header; users present a bearer token. The selector is
        // the single place that decides which audience a request belongs to.
        .AddPolicyScheme(ApiKeyAuthenticationDefaults.SmartScheme, "Bearer or API key", options =>
        {
            options.ForwardDefaultSelector = context =>
                context.Request.Headers.ContainsKey(ApiKeyAuthenticationDefaults.HeaderName)
                    ? ApiKeyAuthenticationDefaults.Scheme
                    : JwtBearerDefaults.AuthenticationScheme;
        })
        .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationDefaults.Scheme,
            "API key",
            _ => { })
        .AddJwtBearer(options =>
        {
            options.SaveToken = true;
            options.RequireHttpsMetadata = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    // A browser cannot set headers on a WebSocket handshake, so SignalR clients
                    // hand the token over in the query string instead. It is read for hub
                    // traffic only: accepting it on ordinary endpoints would put credentials
                    // into access logs and proxy traces for no benefit.
                    if (context.HttpContext.Request.Path.StartsWithSegments("/hubs")
                        && context.Request.Query.TryGetValue("access_token", out var accessToken)
                        && !string.IsNullOrEmpty(accessToken))
                    {
                        context.Token = accessToken.ToString();
                    }

                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    // A valid signature is not the same as a usable account. Deactivating or
                    // deleting a user must take effect on their next request rather than when
                    // their bearer happens to lapse, so the account is re-checked here.
                    var subject = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                    if (!Guid.TryParse(subject, out var userId))
                    {
                        context.Fail("The token does not identify a user.");
                        return;
                    }

                    // A child scope keeps the request's own DbContext pristine: the tenant is
                    // not resolved yet, and a context used before then bakes the empty tenant
                    // into its model for the rest of the request.
                    using var scope = context.HttpContext.RequestServices.CreateScope();
                    var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

                    if (!await users.IsActiveAcrossTenantsAsync(
                            userId,
                            context.HttpContext.RequestAborted))
                    {
                        context.Fail("This account is no longer active.");
                    }
                }
            };
        });

        // One policy per catalogue permission, named exactly after the permission. A controller
        // then states the permission it needs with [Authorize(Policy = PermissionNames.X)],
        // which keeps the endpoint and the permission matrix in one place.
        services.AddAuthorization(options =>
        {
            foreach (var permission in PermissionCatalog.All)
            {
                options.AddPolicy(permission.Name, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(AuthClaimTypes.Permission, permission.Name);
                });
            }
        });

        // The three scheduled passes. Each is a plain scoped service as well as (optionally) a
        // hosted service: the concrete registration is what lets a Hangfire recurring job resolve
        // and call a pass directly, while the hosted registration is the timer path used when
        // Hangfire is not in play. Both call the same public pass methods, so the rules live in
        // one place regardless of who triggers them.
        services.AddScoped<SlaViolationDetectionWorker>();
        services.AddScoped<OutboxProcessor>();
        services.AddScoped<EmailDispatchWorker>();
        services.AddScoped<ScheduledJobs>();

        var hangfireSection = configuration.GetSection(HangfireOptions.SectionName);
        var hangfire = hangfireSection.Get<HangfireOptions>() ?? new HangfireOptions();

        services.Configure<HangfireOptions>(hangfireSection);

        // Each worker keeps its own Enabled switch (a test host turns the timer off), and is only
        // started as a timer when Hangfire is not the scheduler. With Hangfire on, the recurring
        // job calls the same method and the timer must not also run, or every pass would happen
        // twice.
        var slaSection = configuration.GetSection(SlaViolationWorkerOptions.SectionName);
        var slaEnabled = slaSection.Get<SlaViolationWorkerOptions>()?.Enabled ?? true;

        services.Configure<SlaViolationWorkerOptions>(slaSection);

        if (slaEnabled && !hangfire.Enabled)
        {
            services.AddHostedService<SlaViolationDetectionWorker>();
        }

        var outboxSection = configuration.GetSection(OutboxProcessorOptions.SectionName);
        var outboxEnabled = outboxSection.Get<OutboxProcessorOptions>()?.Enabled ?? true;

        services.Configure<OutboxProcessorOptions>(outboxSection);

        if (outboxEnabled && !hangfire.Enabled)
        {
            services.AddHostedService<OutboxProcessor>();
        }

        // The email transport and the sweep that drives it. Credentials bind from Smtp and are
        // never committed - in development they come from user-secrets, in deployment from the
        // environment. With the transport switched off the sweep still runs but leaves the
        // queue untouched, so an installation that has not configured SMTP yet loses nothing.
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));

        var emailSection = configuration.GetSection(EmailDispatchOptions.SectionName);
        var emailEnabled = emailSection.Get<EmailDispatchOptions>()?.Enabled ?? true;

        services.Configure<EmailDispatchOptions>(emailSection);

        if (emailEnabled && !hangfire.Enabled)
        {
            services.AddHostedService<EmailDispatchWorker>();
        }

        // The webhook transport and the sweep that drives it. The transport is an HTTP client
        // of its own, because a call to somebody else's server is a different kind of thing
        // from a call to a database: it has a timeout, a status code and a body, and none of
        // those belong to the retry rules the sweep applies.
        services.AddScoped<IWebhookSender, HttpWebhookSender>();

        var webhookSection = configuration.GetSection(WebhookDispatchOptions.SectionName);
        var webhookOptions = webhookSection.Get<WebhookDispatchOptions>() ?? new WebhookDispatchOptions();

        services.Configure<WebhookDispatchOptions>(webhookSection);

        services.AddHttpClient(HttpWebhookSender.HttpClientName, client =>
        {
            client.Timeout = webhookOptions.Timeout;
        });

        if (webhookOptions.Enabled && !hangfire.Enabled)
        {
            services.AddHostedService<WebhookDispatchWorker>();
        }

        return services;
    }

    /// <summary>
    /// Wires Hangfire as the scheduler for the three recurring passes (plan, "BackgroundJobs").
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called after <see cref="AddInfrastructure"/> so the storage is set up before any recurring
    /// job is registered. With <see cref="HangfireOptions.Enabled"/> false this does nothing at
    /// all, and the workers fall back to their own timers — which is what the test host relies on.
    /// </para>
    /// <para>
    /// The storage is the application database: a job's schedule and history are operational data,
    /// and giving them their own database would mean their own backup and their own failure mode
    /// for no benefit at this size.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddScheduledJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection(HangfireOptions.SectionName).Get<HangfireOptions>()
            ?? new HangfireOptions();

        if (!options.Enabled)
        {
            return services;
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddHangfire(configuration => configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                // Hangfire's own schema is created and upgraded by the library rather than by an
                // EF migration, so the two migration histories never have to agree on it.
                PrepareSchemaIfNecessary = true
            }));

        // One server per process. It is what actually executes the jobs; without it recurring
        // jobs would be enqueued and never picked up.
        services.AddHangfireServer(server => server.WorkerCount = 2);

        // A filter rather than a per-job attribute so the retry policy is set once.
        GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute
        {
            Attempts = options.MaxAutomaticRetries
        });

        return services;
    }
}
