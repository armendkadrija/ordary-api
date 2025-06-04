namespace Odary.Api.Common.Pagination;

public class PaginatedRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public int Skip => (Page - 1) * PageSize;
    public int Take => PageSize;
} 