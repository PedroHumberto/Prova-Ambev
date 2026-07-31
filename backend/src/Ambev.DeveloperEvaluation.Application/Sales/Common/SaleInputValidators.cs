using FluentValidation;
using System.Linq.Expressions;

namespace Ambev.DeveloperEvaluation.Application.Sales.Common;

internal static class SaleInputValidators
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
        validator.RuleFor(saleNumber).NotEmpty().MaximumLength(50);
        validator.RuleFor(saleDate)
            .Must(value => value.Kind == DateTimeKind.Utc)
            .WithMessage("Sale date must be a UTC instant.");
        validator.RuleFor(customerId).NotEmpty();
        validator.RuleFor(customerName).NotEmpty().MaximumLength(200);
        validator.RuleFor(branchId).NotEmpty();
        validator.RuleFor(branchName).NotEmpty().MaximumLength(200);
    }

    internal static void AddItemRules<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, Guid>> productId,
        Expression<Func<T, string>> productName,
        Expression<Func<T, int>> quantity,
        Expression<Func<T, decimal>> unitPrice)
    {
        validator.RuleFor(productId).NotEmpty();
        validator.RuleFor(productName).NotEmpty().MaximumLength(200);
        validator.RuleFor(quantity).InclusiveBetween(1, 20);
        validator.RuleFor(unitPrice).GreaterThan(0).PrecisionScale(18, 2, false);
    }
}
