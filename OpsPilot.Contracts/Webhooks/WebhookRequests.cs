using System.ComponentModel.DataAnnotations;

namespace OpsPilot.Contracts.Webhooks;

/// <summary>
/// Registers a webhook endpoint. The signing secret is minted by the server and returned
/// once — the caller does not choose it, so nobody can register an endpoint they already
/// know the secret of.
/// </summary>
public sealed class CreateWebhookRequest
{
    /// <summary>The absolute URL that receives the JSON payload.</summary>
    [Required]
    [StringLength(500, MinimumLength = 8)]
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Edits a registered endpoint. The URL is the only thing that can be changed: the secret is
/// replaced by rotating it, which is a separate step on purpose.
/// </summary>
public sealed class UpdateWebhookRequest
{
    [Required]
    [StringLength(500, MinimumLength = 8)]
    public string Url { get; set; } = string.Empty;

    /// <summary>Whether the endpoint should keep receiving deliveries.</summary>
    public bool IsActive { get; set; } = true;
}
