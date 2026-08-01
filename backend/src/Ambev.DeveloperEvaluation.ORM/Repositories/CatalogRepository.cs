using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ambev.DeveloperEvaluation.ORM.Repositories;

public sealed class CatalogRepository(DefaultContext context) : ICatalogRepository
{
    private readonly DefaultContext _context = context;

    public async Task<IReadOnlyList<CatalogItemSnapshot>> SearchCustomersAsync(
        string? search,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        var query = _context.Sales.AsNoTracking();
        if (search is not null)
            query = query.Where(sale => EF.Functions.ILike(sale.CustomerName, ContainsPattern(search), "\\"));

        var items = await query
            .Select(sale => new { Id = sale.CustomerId, Name = sale.CustomerName })
            .Distinct()
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return items.Select(item => new CatalogItemSnapshot(item.Id, item.Name)).ToList();
    }

    public async Task<IReadOnlyList<CatalogItemSnapshot>> SearchBranchesAsync(
        string? search,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        var query = _context.Sales.AsNoTracking();
        if (search is not null)
            query = query.Where(sale => EF.Functions.ILike(sale.BranchName, ContainsPattern(search), "\\"));

        var items = await query
            .Select(sale => new { Id = sale.BranchId, Name = sale.BranchName })
            .Distinct()
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return items.Select(item => new CatalogItemSnapshot(item.Id, item.Name)).ToList();
    }

    public async Task<IReadOnlyList<CatalogItemSnapshot>> SearchProductsAsync(
        string? search,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        var query = _context.Set<SaleItem>().AsNoTracking();
        if (search is not null)
            query = query.Where(item => EF.Functions.ILike(item.ProductName, ContainsPattern(search), "\\"));

        var items = await query
            .Select(item => new { Id = item.ProductId, Name = item.ProductName })
            .Distinct()
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return items.Select(item => new CatalogItemSnapshot(item.Id, item.Name)).ToList();
    }

    private static string ContainsPattern(string search) =>
        $"%{search.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)}%";

    private static void ValidateLimit(int limit) => ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
}
