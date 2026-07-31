using Ambev.DeveloperEvaluation.Application.Sales.Common;

namespace Ambev.DeveloperEvaluation.Application.Sales.ListSales;

public sealed class ListSalesResult
{
    public IReadOnlyList<ListSalesItemResult> Items { get; init; } = [];
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}

public sealed class ListSalesItemResult : SaleSummaryResult;
