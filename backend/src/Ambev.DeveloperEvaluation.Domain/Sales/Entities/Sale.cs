using System.Collections.ObjectModel;
using Ambev.DeveloperEvaluation.Domain.Sales.Enums;
using Ambev.DeveloperEvaluation.Domain.Sales.Events;
using Ambev.DeveloperEvaluation.Domain.Sales.Exceptions;

namespace Ambev.DeveloperEvaluation.Domain.Sales.Entities;

public sealed class Sale
{
    private const decimal MaximumMonetaryValue = 9999999999999999.99m;
    private readonly List<SaleItem> _items = [];
    private readonly List<IDomainEvent> _domainEvents = [];
    private readonly ReadOnlyCollection<SaleItem> _readOnlyItems;
    private readonly ReadOnlyCollection<IDomainEvent> _readOnlyDomainEvents;
    private TimeProvider _timeProvider = TimeProvider.System;

    private Sale()
    {
        _readOnlyItems = _items.AsReadOnly();
        _readOnlyDomainEvents = _domainEvents.AsReadOnly();
    }

    public Guid Id { get; private set; }

    public string SaleNumber { get; private set; } = string.Empty;

    public DateTime SaleDate { get; private set; }

    public Guid CustomerId { get; private set; }

    public string CustomerName { get; private set; } = string.Empty;

    public Guid BranchId { get; private set; }

    public string BranchName { get; private set; } = string.Empty;

    public SaleStatus Status { get; private set; }

    public decimal Subtotal { get; private set; }

    public decimal DiscountAmount { get; private set; }

    public decimal TotalAmount { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    public IReadOnlyCollection<SaleItem> Items => _readOnlyItems;

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _readOnlyDomainEvents;

    public bool IsActive => Status == SaleStatus.Active;

    public static Sale Create(
        string saleNumber,
        DateTime saleDate,
        Guid customerId,
        string customerName,
        Guid branchId,
        string branchName,
        IEnumerable<SaleItemInput> items,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        var header = PrepareHeader(saleNumber, saleDate, customerId, customerName, branchId, branchName);
        var itemInputs = items.ToList();
        if (itemInputs.Count == 0)
        {
            throw new InvalidSaleException("A sale must contain at least one active item.");
        }

        if (itemInputs.Any(item => item is null))
        {
            throw new InvalidSaleItemException("Sale items cannot contain null values.");
        }

        EnsureUniqueProducts(itemInputs.Select(item => item.ProductId));

        var provider = timeProvider ?? TimeProvider.System;
        var occurredAt = provider.GetUtcNow().UtcDateTime;
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            _timeProvider = provider,
            Status = SaleStatus.Active,
            CreatedAt = occurredAt,
            UpdatedAt = occurredAt
        };

        sale.ApplyHeader(header);

        foreach (var input in itemInputs)
        {
            sale._items.Add(new SaleItem(
                Guid.NewGuid(),
                input.ProductId,
                input.ProductName,
                input.Quantity,
                input.UnitPrice,
                occurredAt));
        }

        sale.RecalculateTotals();
        sale._domainEvents.Add(new SaleCreated(sale.Id, occurredAt));
        return sale;
    }

    public void UpdateHeader(
        string saleNumber,
        DateTime saleDate,
        Guid customerId,
        string customerName,
        Guid branchId,
        string branchName)
    {
        EnsureActive();
        var header = PrepareHeader(saleNumber, saleDate, customerId, customerName, branchId, branchName);
        if (HasHeader(header))
        {
            return;
        }

        var occurredAt = GetUtcNow();
        ApplyHeader(header);
        MarkUpdated(occurredAt);
    }

    public SaleItem AddItem(Guid productId, string productName, int quantity, decimal unitPrice)
    {
        EnsureActive();
        if (_items.Any(item => item.IsActive && item.ProductId == productId))
        {
            throw new DuplicateActiveProductException(productId);
        }

        var values = SaleItem.PrepareValues(productId, productName, quantity, unitPrice);
        CalculateTotals(_items.Where(item => item.IsActive).Select(ToValues).Append(values));

        var occurredAt = GetUtcNow();
        var item = new SaleItem(Guid.NewGuid(), productId, productName, quantity, unitPrice, occurredAt);
        _items.Add(item);
        RecalculateTotals();
        MarkUpdated(occurredAt);
        return item;
    }

    public void UpdateItem(Guid saleItemId, string productName, int quantity, decimal unitPrice)
    {
        EnsureActive();
        var item = FindItem(saleItemId);
        if (!item.IsActive)
        {
            throw new CancelledSaleItemModificationException(saleItemId);
        }

        var values = SaleItem.PrepareValues(item.ProductId, productName, quantity, unitPrice);
        if (item.HasValues(values))
        {
            return;
        }

        CalculateTotals(_items.Where(candidate => candidate.IsActive)
            .Select(candidate => candidate.Id == saleItemId ? values : ToValues(candidate)));

        var occurredAt = GetUtcNow();
        item.Update(values, occurredAt);
        RecalculateTotals();
        MarkUpdated(occurredAt);
    }

    public void CancelItem(Guid saleItemId)
    {
        var item = FindItem(saleItemId);
        if (!item.IsActive)
        {
            return;
        }

        EnsureActive();
        if (_items.Count(candidate => candidate.IsActive) == 1)
        {
            throw new LastActiveSaleItemException();
        }

        var occurredAt = GetUtcNow();
        item.Cancel(occurredAt);
        RecalculateTotals();
        UpdatedAt = occurredAt;
        _domainEvents.Add(new SaleItemCancelled(Id, item.Id, occurredAt));
    }

    public void ReplaceEditableData(
        string saleNumber,
        DateTime saleDate,
        Guid customerId,
        string customerName,
        Guid branchId,
        string branchName,
        IEnumerable<SaleItemReplacement> items)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(items);

        var header = PrepareHeader(saleNumber, saleDate, customerId, customerName, branchId, branchName);
        var replacements = items.ToList();
        if (replacements.Count == 0)
        {
            throw new InvalidSaleException("A sale must contain at least one active item.");
        }

        if (replacements.Any(item => item is null))
        {
            throw new InvalidSaleItemException("Sale items cannot contain null values.");
        }

        EnsureUniqueProducts(replacements.Select(item => item.ProductId));

        var activeItemsById = _items.Where(item => item.IsActive).ToDictionary(item => item.Id);
        var allItemsById = _items.ToDictionary(item => item.Id);
        var suppliedIds = new HashSet<Guid>();
        var plans = new List<ReplacementPlan>(replacements.Count);

        foreach (var replacement in replacements)
        {
            var values = SaleItem.PrepareValues(
                replacement.ProductId,
                replacement.ProductName,
                replacement.Quantity,
                replacement.UnitPrice);

            if (replacement.Id is not Guid itemId)
            {
                if (activeItemsById.Values.Any(item => item.ProductId == replacement.ProductId))
                {
                    throw new InvalidSaleItemException(
                        $"The active item for product '{replacement.ProductId}' must retain its item ID.");
                }

                plans.Add(new ReplacementPlan(null, replacement.ProductId, values));
                continue;
            }

            if (itemId == Guid.Empty)
            {
                throw new InvalidSaleItemException("Sale item ID cannot be empty.");
            }

            if (!suppliedIds.Add(itemId))
            {
                throw new InvalidSaleItemException($"Sale item ID '{itemId}' was supplied more than once.");
            }

            if (!allItemsById.TryGetValue(itemId, out var existingItem))
            {
                throw new SaleItemNotFoundException(itemId);
            }

            if (!activeItemsById.ContainsKey(itemId))
            {
                throw new CancelledSaleItemModificationException(itemId);
            }

            if (existingItem.ProductId != replacement.ProductId)
            {
                throw new InvalidSaleItemException(
                    $"Product ID of sale item '{itemId}' cannot be changed.");
            }

            plans.Add(new ReplacementPlan(existingItem, replacement.ProductId, values));
        }

        CalculateTotals(plans.Select(plan => plan.Values));

        var omittedItems = activeItemsById.Values.Where(item => !suppliedIds.Contains(item.Id)).ToList();
        var changed = !HasHeader(header)
            || omittedItems.Count > 0
            || plans.Any(plan => plan.Item is null || !plan.Item.HasValues(plan.Values));

        if (!changed)
        {
            return;
        }

        var occurredAt = GetUtcNow();
        ApplyHeader(header);

        foreach (var plan in plans)
        {
            if (plan.Item is null)
            {
                _items.Add(new SaleItem(
                    Guid.NewGuid(),
                    plan.ProductId,
                    plan.Values.ProductName,
                    plan.Values.Quantity,
                    plan.Values.UnitPrice,
                    occurredAt));
            }
            else
            {
                plan.Item.Update(plan.Values, occurredAt);
            }
        }

        foreach (var omittedItem in omittedItems)
        {
            omittedItem.Cancel(occurredAt);
            _domainEvents.Add(new SaleItemCancelled(Id, omittedItem.Id, occurredAt));
        }

        RecalculateTotals();
        MarkUpdated(occurredAt);
    }

    public void Cancel()
    {
        if (!IsActive)
        {
            return;
        }

        var occurredAt = GetUtcNow();
        Status = SaleStatus.Cancelled;
        CancelledAt = occurredAt;
        UpdatedAt = occurredAt;
        _domainEvents.Add(new SaleCancelled(Id, occurredAt));
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    private static SaleHeader PrepareHeader(
        string saleNumber,
        DateTime saleDate,
        Guid customerId,
        string customerName,
        Guid branchId,
        string branchName)
    {
        var normalizedSaleNumber = NormalizeRequiredText(saleNumber, "Sale number", 50);
        if (saleDate.Kind != DateTimeKind.Utc)
        {
            throw new InvalidSaleException("Sale date must be a UTC instant.");
        }

        if (customerId == Guid.Empty)
        {
            throw new InvalidSaleException("Customer ID cannot be empty.");
        }

        if (branchId == Guid.Empty)
        {
            throw new InvalidSaleException("Branch ID cannot be empty.");
        }

        return new SaleHeader(
            normalizedSaleNumber,
            saleDate,
            customerId,
            NormalizeRequiredText(customerName, "Customer name", 200),
            branchId,
            NormalizeRequiredText(branchName, "Branch name", 200));
    }

    private static string NormalizeRequiredText(string? value, string fieldName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidSaleException($"{fieldName} is required.");
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new InvalidSaleException($"{fieldName} must not exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static void EnsureUniqueProducts(IEnumerable<Guid> productIds)
    {
        var uniqueProductIds = new HashSet<Guid>();
        foreach (var productId in productIds)
        {
            if (!uniqueProductIds.Add(productId))
            {
                throw new DuplicateActiveProductException(productId);
            }
        }
    }

    private static SaleTotals CalculateTotals(IEnumerable<SaleItemValues> values)
    {
        try
        {
            decimal subtotal = 0;
            decimal discountAmount = 0;
            decimal totalAmount = 0;

            foreach (var value in values)
            {
                subtotal += value.Subtotal;
                discountAmount += value.DiscountAmount;
                totalAmount += value.TotalAmount;

                EnsureTotalRange(subtotal, "sale subtotal");
                EnsureTotalRange(discountAmount, "sale discount amount");
                EnsureTotalRange(totalAmount, "sale total amount");
            }

            return new SaleTotals(subtotal, discountAmount, totalAmount);
        }
        catch (OverflowException exception)
        {
            throw new MonetaryValueOutOfRangeException("sale totals", exception);
        }
    }

    private static void EnsureTotalRange(decimal value, string valueName)
    {
        if (value < 0 || value > MaximumMonetaryValue)
        {
            throw new MonetaryValueOutOfRangeException(valueName);
        }
    }

    private static SaleItemValues ToValues(SaleItem item)
    {
        return new SaleItemValues(
            item.ProductName,
            item.Quantity,
            item.UnitPrice,
            item.DiscountPercentage,
            item.Subtotal,
            item.DiscountAmount,
            item.TotalAmount);
    }

    private void EnsureActive()
    {
        if (!IsActive)
        {
            throw new CancelledSaleModificationException(Id);
        }
    }

    private SaleItem FindItem(Guid saleItemId)
    {
        if (saleItemId == Guid.Empty)
        {
            throw new InvalidSaleItemException("Sale item ID cannot be empty.");
        }

        return _items.SingleOrDefault(item => item.Id == saleItemId)
            ?? throw new SaleItemNotFoundException(saleItemId);
    }

    private bool HasHeader(SaleHeader header)
    {
        return SaleNumber == header.SaleNumber
            && SaleDate == header.SaleDate
            && CustomerId == header.CustomerId
            && CustomerName == header.CustomerName
            && BranchId == header.BranchId
            && BranchName == header.BranchName;
    }

    private void ApplyHeader(SaleHeader header)
    {
        SaleNumber = header.SaleNumber;
        SaleDate = header.SaleDate;
        CustomerId = header.CustomerId;
        CustomerName = header.CustomerName;
        BranchId = header.BranchId;
        BranchName = header.BranchName;
    }

    private void RecalculateTotals()
    {
        var totals = CalculateTotals(_items.Where(item => item.IsActive).Select(ToValues));
        Subtotal = totals.Subtotal;
        DiscountAmount = totals.DiscountAmount;
        TotalAmount = totals.TotalAmount;
    }

    private void MarkUpdated(DateTime occurredAt)
    {
        UpdatedAt = occurredAt;
        _domainEvents.Add(new SaleUpdated(Id, occurredAt));
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }

    private sealed record SaleHeader(
        string SaleNumber,
        DateTime SaleDate,
        Guid CustomerId,
        string CustomerName,
        Guid BranchId,
        string BranchName);

    private sealed record ReplacementPlan(SaleItem? Item, Guid ProductId, SaleItemValues Values);

    private readonly record struct SaleTotals(decimal Subtotal, decimal DiscountAmount, decimal TotalAmount);
}
