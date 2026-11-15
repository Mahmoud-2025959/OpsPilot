namespace OpsPilot.Domain.Exceptions;

/// <summary>
/// The request was well-formed but names something the system cannot act on — for example an
/// event for a service the company has not registered (plan, UC-06, which answers 422 there).
/// </summary>
/// <remarks>
/// Distinct from <see cref="DomainException"/>, which maps to 400: the shape of the request was
/// fine, it is the reference inside it that cannot be resolved.
/// </remarks>
public sealed class UnprocessableEntityException : DomainException
{
    public UnprocessableEntityException(string message)
        : base(message, "unprocessable_entity")
    {
    }
}
