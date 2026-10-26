using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Infrastructure.Webhooks;

/// <summary>
/// Puts a webhook call on the wire over HTTP, signed (plan, notifications group: the webhook
/// channel's delivery step).
/// </summary>
/// <remarks>
/// The transport's whole job is the wire: it signs, it posts, and it says what the endpoint
/// answered. Whether a refusal deserves another attempt is the dispatcher's decision, so the
/// two are told apart here — a 4xx that is not a rate limit will be refused the same way
/// next time, and everything else might not be.
/// </remarks>
public sealed class HttpWebhookSender : IWebhookSender
{
    /// <summary>The name the HTTP client is registered under.</summary>
    public const string HttpClientName = "opspilot-webhooks";

    private readonly IHttpClientFactory _clients;
    private readonly ILogger<HttpWebhookSender> _logger;

    public HttpWebhookSender(IHttpClientFactory clients, ILogger<HttpWebhookSender> logger)
    {
        _clients = clients;
        _logger = logger;
    }

    public async Task SendAsync(
        WebhookDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = _clients.CreateClient(HttpClientName);

        using var message = new HttpRequestMessage(HttpMethod.Post, request.Url)
        {
            Content = new StringContent(request.Body, Encoding.UTF8, "application/json")
        };

        message.Headers.TryAddWithoutValidation(
            WebhookSigner.EventHeader,
            request.EventType);

        message.Headers.TryAddWithoutValidation(
            WebhookSigner.DeliveryHeader,
            request.DeliveryId);

        message.Headers.TryAddWithoutValidation(
            WebhookSigner.TimestampHeader,
            request.Timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));

        message.Headers.TryAddWithoutValidation(
            WebhookSigner.SignatureHeader,
            WebhookSigner.Sign(request.Secret, request.Timestamp, request.Body));

        HttpResponseMessage response;

        try
        {
            response = await client.SendAsync(message, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            // The host did not answer at all. That is worth another try: the endpoint may be
            // down for a moment rather than gone.
            throw new WebhookDeliveryException(
                $"The endpoint could not be reached: {exception.Message}");
        }
        catch (InvalidOperationException exception)
        {
            // The URL itself is wrong, and no number of attempts will fix that.
            throw new WebhookDeliveryException(
                $"The endpoint URL could not be used: {exception.Message}",
                isPermanent: true);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new WebhookDeliveryException(
                    $"The endpoint answered with {(int)response.StatusCode} " +
                    $"({response.StatusCode}).",
                    isPermanent: IsPermanent(response.StatusCode));
            }
        }
    }

    /// <summary>
    /// True when the refusal will keep happening. A rate limit and a request timeout are the
    /// two 4xx answers that mean "not now" rather than "not ever".
    /// </summary>
    private static bool IsPermanent(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            ? false
            : (int)status is >= 400 and < 500;
}
