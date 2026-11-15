using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Features.Webhooks.Commands.CreateWebhook;
using OpsPilot.Application.Features.Webhooks.Commands.DeleteWebhook;
using OpsPilot.Application.Features.Webhooks.Commands.RotateWebhookSecret;
using OpsPilot.Application.Features.Webhooks.Commands.UpdateWebhook;
using OpsPilot.Application.Features.Webhooks.Queries.GetAllWebhooks;
using OpsPilot.Application.Features.Webhooks.Queries.GetWebhook;
using OpsPilot.Application.Features.Webhooks.Queries.GetWebhookDeliveries;
using OpsPilot.Contracts.Webhooks;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Identity;

namespace OpsPilot.API.Controllers;

/// <summary>
/// The webhook channel: the endpoints a company registered to be told about what is
/// happening (plan, notifications group).
/// </summary>
/// <remarks>
/// <para>
/// A webhook is another way to announce what the outbox announces, so a receiver hears about
/// an event the moment it is stored rather than when a queue happens to be drained.
/// </para>
/// <para>
/// Reading and changing the registrations are split into two permissions: seeing that an
/// endpoint exists and where it points is a smaller thing than being able to add one, and
/// the two are not always given to the same people.
/// </para>
/// </remarks>
[Route("api/v1/webhooks")]
public sealed class WebhooksController : BaseController
{
    /// <summary>Pages the company's webhook endpoints, newest first.</summary>
    [HttpGet]
    [Authorize(Policy = PermissionNames.WebhooksRead)]
    public async Task<ActionResult<PagedResponse<WebhookEndpointDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(new GetAllWebhooksQuery(page, pageSize));

        return Ok(result);
    }

    /// <summary>Reads one registered endpoint.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionNames.WebhooksRead)]
    public async Task<ActionResult<Response<WebhookEndpointDto>>> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetWebhookQuery(id));

        return Ok(Success(result));
    }

    /// <summary>
    /// Registers an endpoint and mints its signing secret.
    /// </summary>
    /// <remarks>
    /// The secret is returned once, here. It is also readable afterwards through the list,
    /// but it is returned here because that is the moment the operator needs to paste it into
    /// the other end.
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = PermissionNames.WebhooksManage)]
    public async Task<ActionResult<Response<CreatedWebhookDto>>> Create(
        [FromBody] CreateWebhookRequest request)
    {
        var result = await Mediator.Send(
            new CreateWebhookCommand(CurrentTenant.TenantId, request.Url));

        return Ok(Success(result, BusinessCode.Created));
    }

    /// <summary>Points an endpoint somewhere else, or puts it in and out of service.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionNames.WebhooksManage)]
    public async Task<ActionResult<Response<WebhookEndpointDto>>> Update(
        Guid id,
        [FromBody] UpdateWebhookRequest request)
    {
        var result = await Mediator.Send(
            new UpdateWebhookCommand(id, request.Url, request.IsActive));

        return Ok(Success(result));
    }

    /// <summary>
    /// Replaces the signing secret. The old secret stops working as soon as this is saved.
    /// </summary>
    [HttpPost("{id:guid}/rotate-secret")]
    [Authorize(Policy = PermissionNames.WebhooksManage)]
    public async Task<ActionResult<Response<CreatedWebhookDto>>> RotateSecret(Guid id)
    {
        var result = await Mediator.Send(new RotateWebhookSecretCommand(id));

        return Ok(Success(result));
    }

    /// <summary>Retires an endpoint. The calls already made still point at it.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionNames.WebhooksManage)]
    public async Task<ActionResult<Response<object?>>> Delete(Guid id)
    {
        await Mediator.Send(new DeleteWebhookCommand(id));

        return Ok(Success<object?>(null));
    }

    /// <summary>
    /// Pages the webhook calls the company has made, newest first.
    /// </summary>
    /// <param name="id">Only the calls made to this endpoint, or all of them when absent.</param>
    [HttpGet("{id:guid}/deliveries")]
    [Authorize(Policy = PermissionNames.WebhooksRead)]
    public async Task<ActionResult<PagedResponse<WebhookDeliveryDto>>> GetDeliveries(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(
            new GetWebhookDeliveriesQuery(id, page, pageSize));

        return Ok(result);
    }

    /// <summary>Pages every webhook call the company has made, newest first.</summary>
    [HttpGet("deliveries")]
    [Authorize(Policy = PermissionNames.WebhooksRead)]
    public async Task<ActionResult<PagedResponse<WebhookDeliveryDto>>> GetAllDeliveries(
        [FromQuery] Guid? endpointId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(
            new GetWebhookDeliveriesQuery(endpointId, page, pageSize));

        return Ok(result);
    }
}
