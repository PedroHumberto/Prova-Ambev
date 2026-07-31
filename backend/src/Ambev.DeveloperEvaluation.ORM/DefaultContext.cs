using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Ambev.DeveloperEvaluation.Domain.Sales.Events;
using Ambev.DeveloperEvaluation.Domain.Sales.Exceptions;
using Ambev.DeveloperEvaluation.ORM.Mapping;
using Ambev.DeveloperEvaluation.ORM.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Reflection;

namespace Ambev.DeveloperEvaluation.ORM;

public class DefaultContext : DbContext, IUnitOfWork
{
    private readonly Dictionary<IDomainEvent, OutboxMessage> _capturedDomainEvents =
        new(ReferenceEqualityComparer.Instance);

    public DbSet<User> Users => Set<User>();

    public DbSet<Sale> Sales => Set<Sale>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DefaultContext(DbContextOptions<DefaultContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateUserEmail(exception))
        {
            var email = exception.Entries
                .Select(entry => entry.Entity)
                .OfType<User>()
                .Select(user => user.Email)
                .FirstOrDefault() ?? string.Empty;

            throw new DuplicateUserEmailException(email, exception);
        }
        catch (DbUpdateException exception) when (IsDuplicateSaleNumber(exception))
        {
            var saleNumber = exception.Entries
                .Select(entry => entry.Entity)
                .OfType<Sale>()
                .Select(sale => sale.SaleNumber)
                .FirstOrDefault() ?? string.Empty;

            throw new DuplicateSaleNumberException(saleNumber, exception);
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var capturedSales = CaptureSalesDomainEvents();
        var result = base.SaveChanges(acceptAllChangesOnSuccess);
        ClearCapturedDomainEvents(capturedSales);
        return result;
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        var capturedSales = CaptureSalesDomainEvents();
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        ClearCapturedDomainEvents(capturedSales);
        return result;
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (Database.CurrentTransaction is not null)
            throw new InvalidOperationException("A database transaction is already active.");

        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await CommitAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static bool IsDuplicateUserEmail(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: UserConfiguration.UserEmailUniqueIndexName
        };

    private static bool IsDuplicateSaleNumber(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: SaleConfiguration.SaleNumberUniqueIndexName
        };

    private IReadOnlyCollection<Sale> CaptureSalesDomainEvents()
    {
        var capturedSales = ChangeTracker.Entries<Sale>()
            .Select(entry => entry.Entity)
            .Where(sale => sale.DomainEvents.Count > 0)
            .ToArray();

        var createdAt = DateTime.UtcNow;
        foreach (var domainEvent in capturedSales.SelectMany(sale => sale.DomainEvents))
        {
            if (_capturedDomainEvents.ContainsKey(domainEvent))
                continue;

            var outboxMessage = SalesIntegrationEventMapper.Map(domainEvent, Guid.NewGuid(), createdAt);
            _capturedDomainEvents.Add(domainEvent, outboxMessage);
            OutboxMessages.Add(outboxMessage);
        }

        return capturedSales;
    }

    private void ClearCapturedDomainEvents(IEnumerable<Sale> sales)
    {
        foreach (var sale in sales)
        {
            foreach (var domainEvent in sale.DomainEvents)
                _capturedDomainEvents.Remove(domainEvent);

            sale.ClearDomainEvents();
        }
    }
}

public sealed class DefaultContextFactory : IDesignTimeDbContextFactory<DefaultContext>
{
    public DefaultContext CreateDbContext(string[] args)
    {
        var configuredConnectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection");

        var builder = new DbContextOptionsBuilder<DefaultContext>();
        var connectionString = configuredConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(FindConfigurationDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        builder.UseNpgsql(
            connectionString,
            options => options.MigrationsAssembly(typeof(DefaultContext).Assembly.FullName)
        );

        return new DefaultContext(builder.Options);
    }

    private static string FindConfigurationDirectory()
    {
        var startingDirectories = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var startingDirectory in startingDirectories)
        {
            for (var directory = new DirectoryInfo(startingDirectory);
                 directory is not null;
                 directory = directory.Parent)
            {
                var candidates = new[]
                {
                    directory.FullName,
                    Path.Combine(directory.FullName, "src", "Ambev.DeveloperEvaluation.WebApi"),
                    Path.Combine(directory.FullName, "backend", "src", "Ambev.DeveloperEvaluation.WebApi")
                };

                var match = candidates.FirstOrDefault(candidate =>
                    File.Exists(Path.Combine(candidate, "appsettings.json")));

                if (match is not null)
                    return match;
            }
        }

        throw new FileNotFoundException(
            "Could not locate the WebApi appsettings.json required for EF Core design-time operations.");
    }
}
