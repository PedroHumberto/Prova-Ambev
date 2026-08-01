using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

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
        SaleQueryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentOutOfRangeException.ThrowIfLessThan(criteria.PageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(criteria.PageSize, 1);

        var offset = checked((criteria.PageNumber - 1) * criteria.PageSize);
        var query = ApplyFilters(_context.Sales.AsNoTracking(), criteria);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await ApplyOrdering(query, criteria.Order)
            .Skip(offset)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return new SalePage(items, totalCount);
    }

    private static IQueryable<Sale> ApplyFilters(IQueryable<Sale> query, SaleQueryCriteria criteria)
    {
        if (criteria.SaleNumber is not null)
            query = query.Where(sale => EF.Functions.ILike(sale.SaleNumber, ContainsPattern(criteria.SaleNumber), "\\"));
        if (criteria.SaleDateFrom is not null)
            query = query.Where(sale => sale.SaleDate >= criteria.SaleDateFrom);
        if (criteria.SaleDateTo is not null)
            query = query.Where(sale => sale.SaleDate <= criteria.SaleDateTo);
        if (criteria.CustomerId is not null)
            query = query.Where(sale => sale.CustomerId == criteria.CustomerId);
        if (criteria.CustomerName is not null)
            query = query.Where(sale => EF.Functions.ILike(sale.CustomerName, ContainsPattern(criteria.CustomerName), "\\"));
        if (criteria.BranchId is not null)
            query = query.Where(sale => sale.BranchId == criteria.BranchId);
        if (criteria.BranchName is not null)
            query = query.Where(sale => EF.Functions.ILike(sale.BranchName, ContainsPattern(criteria.BranchName), "\\"));
        if (criteria.Status is not null)
            query = query.Where(sale => sale.Status == criteria.Status);

        return query;
    }

    private static string ContainsPattern(string search) =>
        $"%{search.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)}%";

    private static IOrderedQueryable<Sale> ApplyOrdering(
        IQueryable<Sale> query,
        IReadOnlyList<SaleSortClause> requestedOrder)
    {
        var order = requestedOrder.Count == 0
            ? [new SaleSortClause(SaleSortField.SaleDate, SortDirection.Descending)]
            : requestedOrder;
        IOrderedQueryable<Sale>? orderedQuery = null;

        foreach (var clause in order)
        {
            orderedQuery = clause.Field switch
            {
                SaleSortField.Id => ApplyOrder(query, orderedQuery, sale => sale.Id, clause.Direction),
                SaleSortField.SaleNumber => ApplyOrder(query, orderedQuery, sale => sale.SaleNumber, clause.Direction),
                SaleSortField.SaleDate => ApplyOrder(query, orderedQuery, sale => sale.SaleDate, clause.Direction),
                SaleSortField.CustomerName => ApplyOrder(query, orderedQuery, sale => sale.CustomerName, clause.Direction),
                SaleSortField.BranchName => ApplyOrder(query, orderedQuery, sale => sale.BranchName, clause.Direction),
                SaleSortField.Status => ApplyOrder(query, orderedQuery, sale => sale.Status, clause.Direction),
                SaleSortField.Subtotal => ApplyOrder(query, orderedQuery, sale => sale.Subtotal, clause.Direction),
                SaleSortField.DiscountAmount => ApplyOrder(query, orderedQuery, sale => sale.DiscountAmount, clause.Direction),
                SaleSortField.TotalAmount => ApplyOrder(query, orderedQuery, sale => sale.TotalAmount, clause.Direction),
                SaleSortField.CreatedAt => ApplyOrder(query, orderedQuery, sale => sale.CreatedAt, clause.Direction),
                SaleSortField.UpdatedAt => ApplyOrder(query, orderedQuery, sale => sale.UpdatedAt, clause.Direction),
                _ => throw new ArgumentOutOfRangeException(nameof(requestedOrder), clause.Field, "Unsupported sale sort field.")
            };
        }

        return order.Any(clause => clause.Field == SaleSortField.Id)
            ? orderedQuery!
            : orderedQuery!.ThenBy(sale => sale.Id);
    }

    private static IOrderedQueryable<Sale> ApplyOrder<TKey>(
        IQueryable<Sale> query,
        IOrderedQueryable<Sale>? orderedQuery,
        Expression<Func<Sale, TKey>> keySelector,
        SortDirection direction)
    {
        if (orderedQuery is null)
        {
            return direction == SortDirection.Descending
                ? query.OrderByDescending(keySelector)
                : query.OrderBy(keySelector);
        }

        return direction == SortDirection.Descending
            ? orderedQuery.ThenByDescending(keySelector)
            : orderedQuery.ThenBy(keySelector);
    }
}
