namespace OpsPilot.Domain.Exceptions;

public sealed class OperationNotPermittedException : DomainException
{
    public OperationNotPermittedException(string message)
        : base(message, "operation_not_permitted")
    {
    }
}