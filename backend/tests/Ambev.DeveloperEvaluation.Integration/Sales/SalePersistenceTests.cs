using System.Collections.Concurrent;
using System.Data.Common;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Ambev.DeveloperEvaluation.Domain.Sales.Enums;
using Ambev.DeveloperEvaluation.Domain.Sales.Exceptions;
using Ambev.DeveloperEvaluation.Integration.Infrastructure;
using Ambev.DeveloperEvaluation.IoC.ModuleInitializers;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Sales;

[Collection(PostgreSqlCollection.Name)]
public sealed class SalePersistenceTests(PostgreSqlFixture fixture)
{
    private static readonly DateTime BaseDate = new(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task MigrateAsync_EmptyPostgreSqlDatabase_CreatesCurrentSalesSchema()
    {
        await using var database = await fixture.CreateDatabaseAsync(migrate: false);
        await using var context = database.CreateContext();

        await context.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT
                EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260730220544_AddSalesPersistence'),
                EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'citext'),
                (SELECT udt_name FROM information_schema.columns WHERE table_name = 'Sales' AND column_name = 'SaleNumber'),
                (SELECT numeric_precision FROM information_schema.columns WHERE table_name = 'SaleItems' AND column_name = 'TotalAmount'),
                (SELECT numeric_scale FROM information_schema.columns WHERE table_name = 'SaleItems' AND column_name = 'TotalAmount')
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
        Assert.Equal("citext", reader.GetString(2));
        Assert.Equal(18, reader.GetInt32(3));
        Assert.Equal(2, reader.GetInt32(4));
    }

    [Fact]
    public async Task Repository_AddAndGetById_RoundTripsAggregateAndDoesNotPersistDomainEvents()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var sale = CreateSale("ROUND-TRIP", BaseDate);
        var cancelledItem = sale.Items.First();
        sale.CancelItem(cancelledItem.Id);
        sale.Cancel();
        Assert.NotEmpty(sale.DomainEvents);

        await using (var writeContext = database.CreateContext())
        {
            var repository = new SaleRepository(writeContext);
            await repository.AddAsync(sale);
            await writeContext.CommitAsync();
        }

        await using var readContext = database.CreateContext();
        var loaded = await new SaleRepository(readContext).GetByIdAsync(sale.Id);

        Assert.NotNull(loaded);
        Assert.Equal("ROUND-TRIP", loaded.SaleNumber);
        Assert.Equal(SaleStatus.Cancelled, loaded.Status);
        Assert.NotNull(loaded.CancelledAt);
        Assert.Equal(2, loaded.Items.Count);
        var loadedCancelledItem = Assert.Single(loaded.Items, item => item.Id == cancelledItem.Id);
        Assert.Equal(SaleStatus.Cancelled, loadedCancelledItem.Status);
        Assert.NotNull(loadedCancelledItem.CancelledAt);
        var activeItem = Assert.Single(loaded.Items, item => item.IsActive);
        Assert.Equal(10, activeItem.Quantity);
        Assert.Equal(20, activeItem.DiscountPercentage);
        Assert.Equal(1000.00m, activeItem.Subtotal);
        Assert.Equal(200.00m, activeItem.DiscountAmount);
        Assert.Equal(800.00m, activeItem.TotalAmount);
        Assert.Equal(activeItem.Subtotal, loaded.Subtotal);
        Assert.Equal(activeItem.DiscountAmount, loaded.DiscountAmount);
        Assert.Equal(activeItem.TotalAmount, loaded.TotalAmount);
        Assert.Empty(loaded.DomainEvents);
        Assert.Empty(readContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task CommitAsync_CaseInsensitiveDuplicateSaleNumber_ThrowsDomainException()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var repository = new SaleRepository(context);
        await repository.AddAsync(CreateSale("Case-Sensitive-Number", BaseDate));
        await context.CommitAsync();
        context.ChangeTracker.Clear();
        await repository.AddAsync(CreateSale("case-sensitive-number", BaseDate.AddMinutes(1)));

        var exception = await Assert.ThrowsAsync<DuplicateSaleNumberException>(() => context.CommitAsync());

        Assert.Contains("case-sensitive-number", exception.Message, StringComparison.Ordinal);
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException?.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
        Assert.Equal("UX_Sales_SaleNumber", postgresException.ConstraintName);
    }

    [Fact]
    public async Task SaleItemRelationship_OrphanInsertAndPrincipalDelete_AreRestrictedByForeignKey()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var sale = CreateSale("FK-RESTRICT", BaseDate);
        await new SaleRepository(context).AddAsync(sale);
        await context.CommitAsync();

        var orphanException = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertItemAsync(context, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Guid.NewGuid(), SaleStatus.Active));
        var deleteException = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"Sales\" WHERE \"Id\" = {sale.Id}"));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, orphanException.SqlState);
        Assert.Equal("FK_SaleItems_Sales_SaleId", orphanException.ConstraintName);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, deleteException.SqlState);
        Assert.Equal("FK_SaleItems_Sales_SaleId", deleteException.ConstraintName);
    }

    [Fact]
    public async Task ActiveProductIndex_DuplicateActiveItemIsRejectedButCancelledHistoryIsAllowed()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var sale = CreateSale("PARTIAL-INDEX", BaseDate);
        await new SaleRepository(context).AddAsync(sale);
        await context.CommitAsync();
        var productId = sale.Items.First().ProductId;

        await InsertItemAsync(context, sale.Id, productId, SaleStatus.Cancelled);
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertItemAsync(context, sale.Id, productId, SaleStatus.Active));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
        Assert.Equal("UX_SaleItems_SaleId_ProductId_Active", exception.ConstraintName);
        Assert.Equal(2, await context.Database.SqlQuery<int>(
            $"SELECT CAST(COUNT(*) AS integer) AS \"Value\" FROM \"SaleItems\" WHERE \"SaleId\" = {sale.Id} AND \"ProductId\" = {productId}").SingleAsync());
    }

    [Fact]
    public async Task DatabaseChecks_InvalidQuantityAndInconsistentTotals_AreRejected()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var sale = CreateSale("CHECKS", BaseDate);
        await new SaleRepository(context).AddAsync(sale);
        await context.CommitAsync();
        var itemId = sale.Items.First().Id;

        var quantityException = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"SaleItems\" SET \"Quantity\" = {0} WHERE \"Id\" = {itemId}"));
        var totalException = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Sales\" SET \"TotalAmount\" = \"TotalAmount\" + {1m} WHERE \"Id\" = {sale.Id}"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, quantityException.SqlState);
        Assert.Equal("CK_SaleItems_Quantity", quantityException.ConstraintName);
        Assert.Equal(PostgresErrorCodes.CheckViolation, totalException.SqlState);
        Assert.Equal("CK_Sales_MonetaryValues", totalException.ConstraintName);
    }

    [Fact]
    public async Task GetPageAsync_SecondPage_UsesStableServerSidePagingAndNoTracking()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using (var writeContext = database.CreateContext())
        {
            var repository = new SaleRepository(writeContext);
            for (var index = 0; index < 5; index++)
                await repository.AddAsync(CreateSale($"PAGE-{index}", BaseDate.AddDays(index)));
            await writeContext.CommitAsync();
        }

        var commands = new CommandCaptureInterceptor();
        await using var readContext = database.CreateContext(commands);
        var page = await new SaleRepository(readContext).GetPageAsync(2, 2);

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(["PAGE-2", "PAGE-1"], page.Items.Select(item => item.SaleNumber));
        Assert.All(page.Items, sale => Assert.Equal(2, sale.Items.Count));
        Assert.Empty(readContext.ChangeTracker.Entries());
        Assert.Contains(commands.Commands, sql =>
            sql.Contains("ORDER BY s.\"SaleDate\" DESC, s.\"Id\" DESC", StringComparison.Ordinal)
            && sql.Contains("LIMIT", StringComparison.Ordinal)
            && sql.Contains("OFFSET", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_OperationFails_RollsBackWholeAggregate()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var sale = CreateSale("ROLLBACK", BaseDate);
        await using (var context = database.CreateContext())
        {
            var repository = new SaleRepository(context);
            await Assert.ThrowsAsync<ExpectedTransactionFailure>(() =>
                context.ExecuteInTransactionAsync<int>(async cancellationToken =>
                {
                    await repository.AddAsync(sale, cancellationToken);
                    throw new ExpectedTransactionFailure();
                }));
        }

        await using var verificationContext = database.CreateContext();
        Assert.Null(await new SaleRepository(verificationContext).GetByIdAsync(sale.Id));
        Assert.Equal(0, await verificationContext.Sales.CountAsync());
    }

    [Fact]
    public async Task GetByIdForUpdateAsync_ConcurrentTransaction_HoldsPostgreSqlRowLock()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var sale = CreateSale("ROW-LOCK", BaseDate);
        await using (var setupContext = database.CreateContext())
        {
            await new SaleRepository(setupContext).AddAsync(sale);
            await setupContext.CommitAsync();
        }

        await using var firstContext = database.CreateContext();
        await using var firstTransaction = await firstContext.Database.BeginTransactionAsync();
        Assert.NotNull(await new SaleRepository(firstContext).GetByIdForUpdateAsync(sale.Id));

        await using var secondContext = database.CreateContext();
        await using var secondTransaction = await secondContext.Database.BeginTransactionAsync();
        await secondContext.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '250ms'");
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SaleRepository(secondContext).GetByIdForUpdateAsync(sale.Id));

        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.LockNotAvailable, postgresException.SqlState);
        await firstTransaction.RollbackAsync();
    }

    [Fact]
    public async Task InfrastructureRegistration_WithinScope_UsesSharedContextForRepositoryAndUnitOfWork()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddDbContext<DefaultContext>(options => options.UseNpgsql(database.ConnectionString));
        new InfrastructureModuleInitializer().Initialize(builder);
        await using var provider = builder.Services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<DefaultContext>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var repository = scope.ServiceProvider.GetRequiredService<ISaleRepository>();

        Assert.Same(context, dbContext);
        Assert.Same(context, unitOfWork);
        Assert.IsType<SaleRepository>(repository);
    }

    private static Sale CreateSale(string number, DateTime saleDate) => Sale.Create(
        number,
        saleDate,
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "Customer snapshot",
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        "Branch snapshot",
        [
            new SaleItemInput(Guid.NewGuid(), "Product without discount", 3, 10.25m),
            new SaleItemInput(Guid.NewGuid(), "Product with discount", 10, 100.00m)
        ],
        new FixedTimeProvider(BaseDate));

    private static Task<int> InsertItemAsync(
        DefaultContext context,
        Guid saleId,
        Guid productId,
        SaleStatus status)
    {
        var now = BaseDate;
        DateTime? cancelledAt = status == SaleStatus.Cancelled ? now : null;
        return context.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO "SaleItems"
                ("Id", "SaleId", "ProductId", "ProductName", "Quantity", "UnitPrice",
                 "DiscountPercentage", "Subtotal", "DiscountAmount", "TotalAmount",
                 "Status", "CreatedAt", "UpdatedAt", "CancelledAt")
            VALUES
                ({{Guid.NewGuid()}}, {{saleId}}, {{productId}}, {{"Raw product"}}, {{1}}, {{10m}},
                 {{0}}, {{10m}}, {{0m}}, {{10m}}, {{status.ToString()}}, {{now}}, {{now}}, {{cancelledAt}})
            """);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class ExpectedTransactionFailure : Exception;

    private sealed class CommandCaptureInterceptor : DbCommandInterceptor
    {
        public ConcurrentQueue<string> Commands { get; } = new();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Enqueue(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
