using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

public sealed class SaleIdRequest
{
    public Guid Id { get; init; }
}

public sealed class CancelSaleItemRequest
{
    public Guid SaleId { get; init; }
    public Guid ItemId { get; init; }
}

public sealed class ListSalesRequest
{
    [FromQuery(Name = "_page")]
    public int PageNumber { get; init; } = 1;

    [FromQuery(Name = "_size")]
    public int PageSize { get; init; } = 10;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateSaleRequest
{
    public string SaleNumber { get; init; } = string.Empty;
    public DateTime SaleDate { get; init; }
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public Guid BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public IReadOnlyCollection<CreateSaleItemRequest> Items { get; init; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateSaleItemRequest
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateSaleRequest
{
    public string SaleNumber { get; init; } = string.Empty;
    public DateTime SaleDate { get; init; }
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public Guid BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public IReadOnlyCollection<UpdateSaleItemRequest> Items { get; init; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateSaleItemRequest
{
    public Guid? Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}
