using FluentValidation;

namespace Ambev.DeveloperEvaluation.Application.Sales.ListSales;

public sealed class ListSalesQueryValidator : AbstractValidator<ListSalesQuery>
{
    public ListSalesQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, ListSalesQuery.MaximumPageSize);
        RuleFor(query => query)
            .Must(query => query.PageNumber < 1
                || query.PageSize < 1
                || (long)(query.PageNumber - 1) * query.PageSize <= int.MaxValue)
            .WithMessage("The requested page offset exceeds the supported range.");
    }
}
