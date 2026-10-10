namespace OpsPilot.Contracts.Wrappers;

public class Response<T>
{
    public T? Data { get; set; }

    public int Code { get; set; }

    public List<string> Errors { get; set; } = [];

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Guid CorrelationId { get; set; }

    public bool Succeeded { get; set; }

    public static Response<T> Success(T data, int code = BusinessCode.Success)
    {
        return new Response<T>
        {
            Data = data,
            Code = code,
            Succeeded = true
        };
    }

    public static Response<T> Failure(List<string> errors, int code = BusinessCode.InternalError)
    {
        return new Response<T>
        {
            Errors = errors,
            Code = code,
            Succeeded = false
        };
    }

    public static Response<T> Failure(string error, int code = BusinessCode.InternalError)
    {
        return new Response<T>
        {
            Errors = [error],
            Code = code,
            Succeeded = false
        };
    }
}

public class PagedResponse<T> : Response<IReadOnlyList<T>>
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public static PagedResponse<T> Create(
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        int totalCount,
        int code = BusinessCode.Success)
    {
        var totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResponse<T>
        {
            Data = items,
            Code = code,
            Succeeded = true,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }
}
