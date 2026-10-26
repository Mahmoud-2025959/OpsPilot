using OpsPilot.Contracts.ApiKeys;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Common.Mappings;

public static class ApiKeyMappings
{
    /// <summary>
    /// Projects a key for listing. The hash never leaves the database; the raw secret does, but
    /// only because the list is behind the permission that issues keys in the first place.
    /// </summary>
    /// <param name="k">The key as it is stored.</param>
    /// <param name="rawKey">
    /// The decrypted secret, or null when it cannot be recovered — a key issued before the
    /// secret was stored, or one encrypted with a key that has since been rotated away.
    /// </param>
    public static ApiKeyDto ToDto(this ApiKey k, string? rawKey = null) =>
        new(k.Id,
            k.TenantId,
            k.Name,
            k.KeyPrefix,
            k.Status.ToString(),
            k.IsActive,
            k.ExpiresAt,
            k.LastUsedAt,
            k.CreatedAt,
            rawKey);
}
