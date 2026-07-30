using FluentValidation;

namespace Ambev.DeveloperEvaluation.Application.Sales.GetSaleById;

public sealed class GetSaleByIdQueryValidator : AbstractValidator<GetSaleByIdQuery>
{
    public GetSaleByIdQueryValidator()
    {
        RuleFor(query => query.Id).NotEmpty();
    }
}
