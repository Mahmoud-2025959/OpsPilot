using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.API.Middleware;
using OpsPilot.Contracts.Wrappers;

namespace OpsPilot.API;

[ApiController]
[Authorize]
public abstract class BaseController : ControllerBase
{
    private IMediator? _mediator;

    protected IMediator Mediator =>
        _mediator ??= HttpContext.RequestServices.GetRequiredService<IMediator>();

    protected Guid CorrelationId =>
        HttpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value) && value is Guid id
            ? id
            : Guid.Empty;

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
