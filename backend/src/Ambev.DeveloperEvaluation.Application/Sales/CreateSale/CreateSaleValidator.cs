using Ambev.DeveloperEvaluation.Application.Sales.Common;
using FluentValidation;

namespace Ambev.DeveloperEvaluation.Application.Sales.CreateSale;

public sealed class CreateSaleCommandValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleCommandValidator()
    {
        SaleInputValidators.AddHeaderRules(
            this,
            command => command.SaleNumber,
            command => command.SaleDate,
            command => command.CustomerId,
            command => command.CustomerName,
            command => command.BranchId,
            command => command.BranchName);

        RuleFor(command => command.Items).NotEmpty();
        RuleForEach(command => command.Items)
            .NotNull()
            .SetValidator(new CreateSaleItemValidator());
    }
}

public sealed class CreateSaleItemValidator : AbstractValidator<CreateSaleItem>
{
    public CreateSaleItemValidator()
    {
        SaleInputValidators.AddItemRules(
            this,
            item => item.ProductId,
            item => item.ProductName,
            item => item.Quantity,
            item => item.UnitPrice);
    }
}
