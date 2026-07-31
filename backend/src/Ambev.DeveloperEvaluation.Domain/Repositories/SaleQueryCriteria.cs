using Ambev.DeveloperEvaluation.Domain.Sales.Enums;

namespace Ambev.DeveloperEvaluation.Domain.Repositories;

public sealed record SaleQueryCriteria
{
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public string? SaleNumber { get; init; }
    public DateTime? SaleDateFrom { get; init; }
    public DateTime? SaleDateTo { get; init; }
    public Guid? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public Guid? BranchId { get; init; }
    public string? BranchName { get; init; }
    public SaleStatus? Status { get; init; }
    public IReadOnlyList<SaleSortClause> Order { get; init; } = [];
}

public sealed record SaleSortClause(SaleSortField Field, SortDirection Direction);

public enum SaleSortField
{
    Id,
    SaleNumber,
    SaleDate,
    CustomerName,
    BranchName,
    Status,
    Subtotal,
    DiscountAmount,
    TotalAmount,
    CreatedAt,
    UpdatedAt
}

public enum SortDirection
{
    Ascending,
    Descending
}
