using Ambev.DeveloperEvaluation.Domain.Repositories;
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
        RuleFor(query => query.SaleDateFrom)
            .Must(BeUtcWhenSpecified)
            .WithMessage("Sale date from must be a UTC instant.");
        RuleFor(query => query.SaleDateTo)
            .Must(BeUtcWhenSpecified)
            .WithMessage("Sale date to must be a UTC instant.");
        RuleFor(query => query)
            .Must(query => query.SaleDateFrom is null
                || query.SaleDateTo is null
                || query.SaleDateFrom <= query.SaleDateTo)
            .WithMessage("Sale date from must not be later than sale date to.");
        RuleFor(query => query.Status)
            .Must(status => status is null || Enum.IsDefined(status.Value));
        RuleFor(query => query.CustomerId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("Customer ID cannot be empty when specified.");
        RuleFor(query => query.BranchId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("Branch ID cannot be empty when specified.");
        RuleFor(query => query.SaleNumber)
            .Must(BeNonBlankWhenSpecified)
            .WithMessage("Sale number must not be empty when specified.");
        RuleFor(query => query.CustomerName)
            .Must(BeNonBlankWhenSpecified)
            .WithMessage("Customer name must not be empty when specified.");
        RuleFor(query => query.BranchName)
            .Must(BeNonBlankWhenSpecified)
            .WithMessage("Branch name must not be empty when specified.");
        RuleFor(query => query.Order)
            .NotNull()
            .WithMessage("Order must not be null.");
        When(query => query.Order is not null, () =>
        {
            RuleForEach(query => query.Order)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage("Order clause must not be null.")
                .ChildRules(clause =>
                {
                    clause.RuleFor(value => value.Field).IsInEnum();
                    clause.RuleFor(value => value.Direction).IsInEnum();
                });
            RuleFor(query => query.Order)
                .Must(HaveUniqueNonNullFields)
                .WithMessage("Order fields must not be repeated.");
        });
    }

    private static bool BeUtcWhenSpecified(DateTime? value) =>
        value is null || value.Value.Kind == DateTimeKind.Utc;

    private static bool BeNonBlankWhenSpecified(string? value) =>
        value is null || !string.IsNullOrWhiteSpace(value);

    private static bool HaveUniqueNonNullFields(IReadOnlyList<SaleSortClause> order)
    {
        var fields = order
            .Where(clause => clause is not null)
            .Select(clause => clause.Field)
            .ToList();
        return fields.Distinct().Count() == fields.Count;
    }
}
