using Ambev.DeveloperEvaluation.Domain.Sales.Enums;
using Ambev.DeveloperEvaluation.Domain.Sales.Exceptions;

namespace Ambev.DeveloperEvaluation.Domain.Sales.Entities;

public sealed class SaleItem
{
    private const decimal MaximumMonetaryValue = 9999999999999999.99m;

    private SaleItem()
    {
    }

    internal SaleItem(Guid id, Guid productId, string productName, int quantity, decimal unitPrice, DateTime occurredAt)
    {
        if (id == Guid.Empty)
        {
            throw new InvalidSaleItemException("Sale item ID cannot be empty.");
        }

        var values = PrepareValues(productId, productName, quantity, unitPrice);

        Id = id;
        ProductId = productId;
        Apply(values);
        Status = SaleStatus.Active;
        CreatedAt = occurredAt;
        UpdatedAt = occurredAt;
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public int DiscountPercentage { get; private set; }

    public decimal Subtotal { get; private set; }

    public decimal DiscountAmount { get; private set; }

    public decimal TotalAmount { get; private set; }

    public SaleStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    public bool IsActive => Status == SaleStatus.Active;

    internal static SaleItemValues PrepareValues(
        Guid productId,
        string productName,
        int quantity,
        decimal unitPrice)
    {
        if (productId == Guid.Empty)
        {
            throw new InvalidSaleItemException("Product ID cannot be empty.");
        }

        var normalizedProductName = NormalizeRequiredName(productName, "Product name");

        if (quantity is < 1 or > 20)
        {
            throw new InvalidSaleItemException("Quantity must be between 1 and 20.");
        }

        if (unitPrice <= 0 || unitPrice > MaximumMonetaryValue)
        {
            throw new InvalidSaleItemException(
                $"Unit price must be between 0.01 and {MaximumMonetaryValue}.");
        }

        if (GetDecimalScale(unitPrice) > 2)
        {
            throw new InvalidSaleItemException("Unit price must have at most two decimal places.");
        }

        var discountPercentage = quantity switch
        {
            <= 3 => 0,
            <= 9 => 10,
            _ => 20
        };

        try
        {
            var subtotal = Round(unitPrice * quantity);
            EnsureStoredRange(subtotal, "item subtotal");

            var discountAmount = Round(subtotal * discountPercentage / 100m);
            EnsureStoredRange(discountAmount, "item discount amount");

            var totalAmount = Round(subtotal - discountAmount);
            EnsureStoredRange(totalAmount, "item total amount");

            return new SaleItemValues(
                normalizedProductName,
                quantity,
                unitPrice,
                discountPercentage,
                subtotal,
                discountAmount,
                totalAmount);
        }
        catch (OverflowException exception)
        {
            throw new MonetaryValueOutOfRangeException("sale item", exception);
        }
    }

    internal bool HasValues(SaleItemValues values)
    {
        return ProductName == values.ProductName
            && Quantity == values.Quantity
            && UnitPrice == values.UnitPrice;
    }

    internal void Update(SaleItemValues values, DateTime occurredAt)
    {
        if (!IsActive)
        {
            throw new CancelledSaleItemModificationException(Id);
        }

        if (HasValues(values))
        {
            return;
        }

        Apply(values);
        UpdatedAt = occurredAt;
    }

    internal void Cancel(DateTime occurredAt)
    {
        if (!IsActive)
        {
            return;
        }

        Status = SaleStatus.Cancelled;
        CancelledAt = occurredAt;
        UpdatedAt = occurredAt;
    }

    private static string NormalizeRequiredName(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidSaleItemException($"{fieldName} is required.");
        }

        var normalized = value.Trim();
        if (normalized.Length > 200)
        {
            throw new InvalidSaleItemException($"{fieldName} must not exceed 200 characters.");
        }

        return normalized;
    }

    private static int GetDecimalScale(decimal value)
    {
        return (decimal.GetBits(value)[3] >> 16) & 0x7F;
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static void EnsureStoredRange(decimal value, string valueName)
    {
        if (value < 0 || value > MaximumMonetaryValue)
        {
            throw new MonetaryValueOutOfRangeException(valueName);
        }
    }

    private void Apply(SaleItemValues values)
    {
        ProductName = values.ProductName;
        Quantity = values.Quantity;
        UnitPrice = values.UnitPrice;
        DiscountPercentage = values.DiscountPercentage;
        Subtotal = values.Subtotal;
        DiscountAmount = values.DiscountAmount;
        TotalAmount = values.TotalAmount;
    }
}

internal readonly record struct SaleItemValues(
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    int DiscountPercentage,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TotalAmount);
