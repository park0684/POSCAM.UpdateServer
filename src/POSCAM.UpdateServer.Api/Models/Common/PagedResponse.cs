namespace POSCAM.UpdateServer.Api.Models.Common;

public sealed class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public long TotalCount { get; init; }

    public int TotalPages { get; init; }
}
