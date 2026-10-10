using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using OpsPilot.Application.Abstractions;

namespace OpsPilot.Infrastructure.Persistence;

/// <summary>
/// Reads the acting user's identifier from the current HTTP context.
/// </summary>
/// <remarks>
/// Returns null outside a request (background jobs, migrations, seeding) so that system
/// actions are audited with a null actor rather than a fabricated one.
/// </remarks>
public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private const string SubjectClaim = "sub";

    public string? UserId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;

            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            return principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue(SubjectClaim);
        }
    }
}
