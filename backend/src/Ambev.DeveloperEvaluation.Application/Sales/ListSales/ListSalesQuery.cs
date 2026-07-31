using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Sales.Enums;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.ListSales;

public sealed record ListSalesQuery : IRequest<ListSalesResult>
{
    public const int DefaultPageNumber = 1;
    public const int DefaultPageSize = 10;
    public const int MaximumPageSize = 100;

    public int PageNumber { get; init; } = DefaultPageNumber;
    public int PageSize { get; init; } = DefaultPageSize;
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
