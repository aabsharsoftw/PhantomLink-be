namespace PhantomPulse.SharedKernel.Contracts;

public sealed record ApiError(string Code, string Message);

public sealed class ApiResponse<T>
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
    public T? Data { get; init; }
    public IReadOnlyList<ApiError> Errors { get; init; } = Array.Empty<ApiError>();

    public static ApiResponse<T> Ok(T data, string message) => new()
    {
        Success = true,
        Message = message,
        Data = data,
        Errors = Array.Empty<ApiError>()
    };

    public static ApiResponse<T> Fail(string message, params ApiError[] errors) => new()
    {
        Success = false,
        Message = message,
        Data = default,
        Errors = errors
    };
}

public sealed class PagedData<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }
    public required int TotalPages { get; init; }
}

public sealed record PaginationQuery(int Page = 1, int PageSize = 25)
{
}

public static class Pagination
{
    public static PagedData<T> Slice<T>(IReadOnlyList<T> source, PaginationQuery query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize switch
        {
            < 1 => 25,
            > 100 => 100,
            _ => query.PageSize
        };
        var total = source.Count;
        var items = source.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);

        return new PagedData<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = totalPages
        };
    }
}
