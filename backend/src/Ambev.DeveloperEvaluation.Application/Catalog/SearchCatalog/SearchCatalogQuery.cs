using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Catalog.SearchCatalog;

public sealed record SearchCatalogQuery(
    CatalogType Type,
    string? Search = null,
    int Limit = 10) : IRequest<IReadOnlyList<CatalogItemResult>>
{
    public const int DefaultLimit = 10;
    public const int MaximumLimit = 100;
}

public enum CatalogType
{
    Customer,
    Branch,
    Product
}

public sealed record CatalogItemResult(Guid Id, string Name);
