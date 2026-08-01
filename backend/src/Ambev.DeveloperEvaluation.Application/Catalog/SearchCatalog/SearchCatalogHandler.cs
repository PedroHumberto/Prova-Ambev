using Ambev.DeveloperEvaluation.Domain.Repositories;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Catalog.SearchCatalog;

public sealed class SearchCatalogHandler(ICatalogRepository catalogRepository)
    : IRequestHandler<SearchCatalogQuery, IReadOnlyList<CatalogItemResult>>
{
    private readonly ICatalogRepository _catalogRepository = catalogRepository
        ?? throw new ArgumentNullException(nameof(catalogRepository));

    public async Task<IReadOnlyList<CatalogItemResult>> Handle(
        SearchCatalogQuery query,
        CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var snapshots = query.Type switch
        {
            CatalogType.Customer => await _catalogRepository.SearchCustomersAsync(
                search, query.Limit, cancellationToken),
            CatalogType.Branch => await _catalogRepository.SearchBranchesAsync(
                search, query.Limit, cancellationToken),
            CatalogType.Product => await _catalogRepository.SearchProductsAsync(
                search, query.Limit, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(query), query.Type, "Unsupported catalog type.")
        };

        return snapshots
            .Select(snapshot => new CatalogItemResult(snapshot.Id, snapshot.Name))
            .ToList();
    }
}
