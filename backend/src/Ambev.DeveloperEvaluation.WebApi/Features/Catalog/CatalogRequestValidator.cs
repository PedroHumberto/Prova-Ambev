using FluentValidation;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Catalog;

public sealed class CatalogRequestValidator : AbstractValidator<CatalogRequest>
{
    public CatalogRequestValidator()
    {
        RuleFor(request => request.Limit).InclusiveBetween(1, 100);
        RuleFor(request => request.Search).MaximumLength(200);
    }
}
