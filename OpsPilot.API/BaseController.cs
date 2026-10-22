using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.API.Middleware;
using OpsPilot.Application.Abstractions;
using OpsPilot.Contracts.Wrappers;

namespace OpsPilot.API;

[ApiController]
[Authorize]
public abstract class BaseController : ControllerBase
{
    private IMediator? _mediator;

    protected IMediator Mediator =>
        _mediator ??= HttpContext.RequestServices.GetRequiredService<IMediator>();

    /// <summary>
    /// The tenant resolved for this request by <see cref="TenantResolutionMiddleware"/>.
    /// Endpoints must take the tenant from here, never from the request body, so a caller
    /// cannot act on a company they do not belong to.
    /// </summary>
    protected ICurrentTenant CurrentTenant =>
        HttpContext.RequestServices.GetRequiredService<ICurrentTenant>();

    protected Guid CorrelationId =>
        HttpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value) && value is Guid id
            ? id
            : Guid.Empty;

    /// <summary>Id of the signed-in user, taken from the token's subject claim.</summary>
    protected Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    protected bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    protected Response<T> Success<T>(T data, int code = BusinessCode.Success)
    {
        var response = Response<T>.Success(data, code);
        response.CorrelationId = CorrelationId;
        return response;
    }

    protected Response<T> Failure<T>(string error, int code)
    {
        var response = Response<T>.Failure(error, code);
        response.CorrelationId = CorrelationId;
        return response;
    }
}
