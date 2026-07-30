using Ambev.DeveloperEvaluation.Application.Sales.Common;
using FluentValidation;

namespace Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;

public sealed class UpdateSaleCommandValidator : AbstractValidator<UpdateSaleCommand>
{
    public UpdateSaleCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
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
            .SetValidator(new UpdateSaleItemValidator());
    }
}

public sealed class UpdateSaleItemValidator : AbstractValidator<UpdateSaleItem>
{
    public UpdateSaleItemValidator()
    {
        RuleFor(item => item.Id)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("Sale item ID cannot be empty when supplied.");

        SaleInputValidators.AddItemRules(
            this,
            item => item.ProductId,
            item => item.ProductName,
            item => item.Quantity,
            item => item.UnitPrice);
    }
}
