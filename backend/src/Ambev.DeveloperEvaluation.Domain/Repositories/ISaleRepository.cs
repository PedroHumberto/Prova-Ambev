using Ambev.DeveloperEvaluation.Domain.Sales.Entities;

namespace Ambev.DeveloperEvaluation.Domain.Repositories;

public interface ISaleRepository
{
    Task AddAsync(Sale sale, CancellationToken cancellationToken = default);

    Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Sale?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SalePage> GetPageAsync(
        SaleQueryCriteria criteria,
        CancellationToken cancellationToken = default);
}

public sealed record SalePage(IReadOnlyList<Sale> Items, int TotalCount);
