using FluentValidation;
using System.Linq.Expressions;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

public sealed class SaleIdRequestValidator : AbstractValidator<SaleIdRequest>
{
    public SaleIdRequestValidator() => RuleFor(request => request.Id).NotEmpty();
}

public sealed class CancelSaleItemRequestValidator : AbstractValidator<CancelSaleItemRequest>
{
    public CancelSaleItemRequestValidator()
    {
        RuleFor(request => request.SaleId).NotEmpty();
        RuleFor(request => request.ItemId).NotEmpty();
    }
}

public sealed class ListSalesRequestValidator : AbstractValidator<ListSalesRequest>
{
    public ListSalesRequestValidator()
    {
        RuleFor(request => request.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(request => request.PageSize).InclusiveBetween(1, 100);
        RuleFor(request => request)
            .Must(request => request.PageNumber < 1
                || request.PageSize < 1
                || (long)(request.PageNumber - 1) * request.PageSize <= int.MaxValue)
            .WithMessage("The requested page offset exceeds the supported range.");
    }
}

public sealed class CreateSaleRequestValidator : AbstractValidator<CreateSaleRequest>
{
    public CreateSaleRequestValidator()
    {
        SalesRequestValidationRules.AddHeaderRules(
            this,
            request => request.SaleNumber,
            request => request.SaleDate,
            request => request.CustomerId,
            request => request.CustomerName,
            request => request.BranchId,
            request => request.BranchName);
        RuleFor(request => request.Items).NotEmpty();
        RuleForEach(request => request.Items)
            .NotNull()
            .SetValidator(new CreateSaleItemRequestValidator());
    }
}

public sealed class CreateSaleItemRequestValidator : AbstractValidator<CreateSaleItemRequest>
{
    public CreateSaleItemRequestValidator() => SalesRequestValidationRules.AddItemRules(
        this,
        item => item.ProductId,
        item => item.ProductName,
        item => item.Quantity,
        item => item.UnitPrice);
}

public sealed class UpdateSaleRequestValidator : AbstractValidator<UpdateSaleRequest>
{
    public UpdateSaleRequestValidator()
    {
        SalesRequestValidationRules.AddHeaderRules(
            this,
            request => request.SaleNumber,
            request => request.SaleDate,
            request => request.CustomerId,
            request => request.CustomerName,
            request => request.BranchId,
            request => request.BranchName);
        RuleFor(request => request.Items).NotEmpty();
        RuleForEach(request => request.Items)
            .NotNull()
            .SetValidator(new UpdateSaleItemRequestValidator());
    }
}

public sealed class UpdateSaleItemRequestValidator : AbstractValidator<UpdateSaleItemRequest>
{
    public UpdateSaleItemRequestValidator()
    {
        RuleFor(item => item.Id)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("Sale item ID cannot be empty when supplied.");
        SalesRequestValidationRules.AddItemRules(
            this,
            item => item.ProductId,
            item => item.ProductName,
            item => item.Quantity,
            item => item.UnitPrice);
    }
}

internal static class SalesRequestValidationRules
{
    internal static void AddHeaderRules<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, string>> saleNumber,
        Expression<Func<T, DateTime>> saleDate,
        Expression<Func<T, Guid>> customerId,
        Expression<Func<T, string>> customerName,
        Expression<Func<T, Guid>> branchId,
        Expression<Func<T, string>> branchName)
    {
        AddTextRule(validator, saleNumber, 50);
        validator.RuleFor(saleDate)
            .Must(value => value.Kind == DateTimeKind.Utc)
            .WithMessage("Sale date must be a UTC instant.");
        validator.RuleFor(customerId).NotEmpty();
        AddTextRule(validator, customerName, 200);
        validator.RuleFor(branchId).NotEmpty();
        AddTextRule(validator, branchName, 200);
    }

    internal static void AddItemRules<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, Guid>> productId,
        Expression<Func<T, string>> productName,
        Expression<Func<T, int>> quantity,
        Expression<Func<T, decimal>> unitPrice)
    {
        validator.RuleFor(productId).NotEmpty();
        AddTextRule(validator, productName, 200);
        validator.RuleFor(quantity).InclusiveBetween(1, 20);
        validator.RuleFor(unitPrice).GreaterThan(0).PrecisionScale(18, 2, false);
    }

    private static void AddTextRule<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, string>> property,
        int maximumLength)
    {
        validator.RuleFor(property)
            .NotEmpty()
            .Must(value => value is null || value.Trim().Length <= maximumLength)
            .WithMessage($"'{{PropertyName}}' must be {maximumLength} characters or fewer after trimming.");
    }
}
