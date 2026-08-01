namespace Ambev.DeveloperEvaluation.Domain.Repositories;

public interface ICatalogRepository
{
    Task<IReadOnlyList<CatalogItemSnapshot>> SearchCustomersAsync(
        string? search,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogItemSnapshot>> SearchBranchesAsync(
        string? search,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogItemSnapshot>> SearchProductsAsync(
        string? search,
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed record CatalogItemSnapshot(Guid Id, string Name);
