using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ambev.DeveloperEvaluation.ORM.Repositories;

public sealed class SaleRepository(DefaultContext context) : ISaleRepository
{
    private readonly DefaultContext _context = context;

    public async Task AddAsync(Sale sale, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sale);
        await _context.Sales.AddAsync(sale, cancellationToken);
    }

    public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Sales
            .AsNoTracking()
            .Include(sale => sale.Items)
            .AsSplitQuery()
            .SingleOrDefaultAsync(sale => sale.Id == id, cancellationToken);

    public async Task<Sale?> GetByIdForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "A transaction must be active before locking a sale for update.");
        }

        var sale = await _context.Sales
            .FromSqlInterpolated($"SELECT * FROM \"Sales\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (sale is not null)
        {
            await _context.Entry(sale)
                .Collection(candidate => candidate.Items)
                .LoadAsync(cancellationToken);
        }

        return sale;
    }

    public async Task<SalePage> GetPageAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var offset = checked((pageNumber - 1) * pageSize);
        var query = _context.Sales.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(sale => sale.SaleDate)
            .ThenByDescending(sale => sale.Id)
            .Skip(offset)
            .Take(pageSize)
            .Include(sale => sale.Items)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new SalePage(items, totalCount);
    }
}
