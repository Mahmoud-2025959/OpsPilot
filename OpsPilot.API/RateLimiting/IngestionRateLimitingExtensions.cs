using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using OpsPilot.Contracts.Auth;

namespace OpsPilot.API.RateLimiting;

/// <summary>
/// Applies the plan's ingestion throttle: a fixed window per API key, answered with a 429
/// problem (plan, UC-06).
/// </summary>
/// <remarks>
/// <para>
/// Only ingestion is counted. The throttle is a policy that endpoints opt into rather than a
/// global one, so the bearer-token surface keeps working without a limit — a human dashboard
/// scrolling through incidents is not traffic that needs policing.
/// </para>
/// <para>
/// The middleware sits after authentication because that is where a request proves which key it
/// carries: the partition is the key's id claim, and everything else — an unknown key, a missing
/// header, a browser — shares a single bucket instead of getting a fresh one per guess.
/// </para>
/// </remarks>
public static class IngestionRateLimitingExtensions
{
    /// <summary>Policy the ingestion endpoint opts into.</summary>
    public const string IngestionPolicy = "ingestion";

    /// <summary>Partition for callers that never proved which key they carry.</summary>
    private const string NoKeyPartition = "no-api-key";

    public static IServiceCollection AddIngestionRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<IngestionRateLimitOptions>(
            configuration.GetSection(IngestionRateLimitOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            // No global policy: nothing is counted unless the endpoint asks to be.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                var problems = context.HttpContext.RequestServices.GetService<IProblemDetailsService>();

                if (problems is null)
                {
                    return;
                }

                await problems.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests",
                        Detail = "This API key has sent more events than the allowed rate. "
                            + "Wait for the current window to pass and try again."
                    }
                });
            };

            options.AddPolicy(IngestionPolicy, context =>
            {
                var limits = context.RequestServices
                    .GetRequiredService<IOptions<IngestionRateLimitOptions>>()
                    .Value;

                return RateLimitPartition.GetFixedWindowLimiter(
                    PartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.PermitLimit,
                        Window = limits.Window,

                        // Nothing is queued: a burst past the limit is refused at once, which is
                        // what a caller can act on, rather than being answered seconds late.
                        QueueLimit = 0
                    });
            });
        });

        return services;
    }

    /// <summary>
    /// One bucket per API key, identified by the id it authenticated with — never by the key
    /// itself, which stays out of the limiter's state entirely.
    /// </summary>
    private static string PartitionKey(HttpContext context) =>
        context.User.FindFirstValue(AuthClaimTypes.ApiKeyId) is { Length: > 0 } apiKeyId
            ? apiKeyId
            : NoKeyPartition;
}
