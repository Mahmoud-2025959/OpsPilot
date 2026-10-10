using System.Text.Json;
using FluentValidation;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.API.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
