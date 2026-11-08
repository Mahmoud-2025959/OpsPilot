using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.API.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly bool _includeDetails;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _includeDetails = environment.IsDevelopment();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            var (statusCode, code, errors) = Map(ex);

            if (statusCode >= StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(ex, "Unhandled exception during request processing.");

                // A 500 has no useful message of its own: the generic sentence says only that
                // something broke. In Development the exception's own text is what a developer
                // needs - and what a test asserting on the body can see. In Production it stays
                // hidden, because a stack trace or a SQL error in a response leaks internals.
                if (_includeDetails)
                {
                    errors = [ex.ToString()];
                }
            }
            else
            {
                _logger.LogWarning("Request failed with {StatusCode}: {Message}", statusCode, ex.Message);
            }

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var response = Response<object?>.Failure(errors, code);
            response.CorrelationId = context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value)
                && value is Guid id
                    ? id
                    : Guid.Empty;

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, SerializerOptions));
        }
    }

    private static (int StatusCode, int Code, List<string> Errors) Map(Exception exception)
    {
        return exception switch
        {
            ValidationException validation => (
                StatusCodes.Status400BadRequest,
                BusinessCode.ValidationError,
                validation.Errors.Select(e => e.ErrorMessage).ToList()),

            EntityNotFoundException notFound => (
                StatusCodes.Status404NotFound,
                BusinessCode.NotFound,
                [notFound.Message]),

            ConflictException conflict => (
                StatusCodes.Status409Conflict,
                BusinessCode.Conflict,
                [conflict.Message]),

            UnauthorizedException unauthorized => (
                StatusCodes.Status401Unauthorized,
                BusinessCode.Unauthorized,
                [unauthorized.Message]),

            ForbiddenException forbidden => (
                StatusCodes.Status403Forbidden,
                BusinessCode.Forbidden,
                [forbidden.Message]),

            // A rejected state change is a conflict with the resource's current state, not a
            // malformed request (plan, "تغيير الحالة"): 409 rather than the generic 400 that
            // DomainException would otherwise produce.
            InvalidStateTransitionException transition => (
                StatusCodes.Status409Conflict,
                BusinessCode.Conflict,
                [transition.Message]),

            // Well-formed but unactionable -- an event naming a service the company has not
            // registered (plan, UC-06). 422 rather than 400 or 404: the request is understood,
            // the reference inside it just cannot be resolved.
            UnprocessableEntityException unprocessable => (
                StatusCodes.Status422UnprocessableEntity,
                BusinessCode.UnprocessableEntity,
                [unprocessable.Message]),

            DomainException domain => (
                StatusCodes.Status400BadRequest,
                BusinessCode.ValidationError,
                [domain.Message]),

            _ => (
                StatusCodes.Status500InternalServerError,
                BusinessCode.InternalError,
                ["An unexpected error occurred while processing the request."])
        };
    }
}
