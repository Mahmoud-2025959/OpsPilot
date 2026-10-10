namespace OpsPilot.Domain.Enums;

public enum OutboxMessageState
{
    Pending,
    Processed,
    Failed
}