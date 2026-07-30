namespace Ambev.DeveloperEvaluation.Domain.Sales.Entities;

public sealed record SaleItemInput(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice);

public sealed record SaleItemReplacement(
    Guid? Id,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice);
