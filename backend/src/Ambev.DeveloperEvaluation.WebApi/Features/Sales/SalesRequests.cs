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
    /// <summary>One-based page number. Defaults to 1.</summary>
    [FromQuery(Name = "_page")]
    public int PageNumber { get; init; } = 1;

    /// <summary>Page size from 1 through 100. Defaults to 10.</summary>
    [FromQuery(Name = "_size")]
    public int PageSize { get; init; } = 10;

    /// <summary>Comma-separated clauses using a whitelisted field and optional asc/desc direction.</summary>
    [FromQuery(Name = "_order")]
    public string? Order { get; init; }

    [FromQuery(Name = "saleNumber")]
    public string? SaleNumber { get; init; }

    [UtcQueryInstant("saleDateFrom")]
    public DateTimeOffset? SaleDateFrom { get; init; }

    [UtcQueryInstant("saleDateTo")]
    public DateTimeOffset? SaleDateTo { get; init; }

    [CanonicalUuidQuery("customerId")]
    public Guid? CustomerId { get; init; }

    [FromQuery(Name = "customerName")]
    public string? CustomerName { get; init; }

    [CanonicalUuidQuery("branchId")]
    public Guid? BranchId { get; init; }

    [FromQuery(Name = "branchName")]
    public string? BranchName { get; init; }

    [FromQuery(Name = "status")]
    public string? Status { get; init; }
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
