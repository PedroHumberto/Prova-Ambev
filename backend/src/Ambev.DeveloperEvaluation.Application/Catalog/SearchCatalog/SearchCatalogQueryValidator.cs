using FluentValidation;

namespace Ambev.DeveloperEvaluation.Application.Catalog.SearchCatalog;

public sealed class SearchCatalogQueryValidator : AbstractValidator<SearchCatalogQuery>
{
    public SearchCatalogQueryValidator()
    {
        RuleFor(query => query.Type).IsInEnum();
        RuleFor(query => query.Limit)
            .InclusiveBetween(1, SearchCatalogQuery.MaximumLimit);
        RuleFor(query => query.Search)
            .MaximumLength(200);
    }
}
