namespace OpsPilot.Contracts.Wrappers;

public static class BusinessCode
{
    public const int Success = 1000;
    public const int Created = 1001;
    public const int NoContent = 1002;

    public const int ValidationError = 2000;
    public const int NotFound = 2001;
    public const int Conflict = 2002;
    public const int Unauthorized = 2003;
    public const int Forbidden = 2004;
    public const int UnprocessableEntity = 2005;

    public const int InternalError = 5000;
    public const int ServiceUnavailable = 5001;
}
