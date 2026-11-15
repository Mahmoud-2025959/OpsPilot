namespace OpsPilot.Domain.Exceptions;

public sealed class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entityName, Guid id)
        : base($"{entityName} with id '{id}' was not found.", "entity_not_found")
    {
    }

    /// <summary>
    /// For entities looked up by something other than an id — an SLA policy by its severity, for
    /// instance — where the caller writes the whole message.
    /// </summary>
    public EntityNotFoundException(string message)
        : base(message, "entity_not_found")
    {
    }
}