using Ambev.DeveloperEvaluation.ORM;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.ORM;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EnvironmentVariableCollection
{
    public const string Name = "Environment variable tests";
}

[Collection(EnvironmentVariableCollection.Name)]
public sealed class DefaultContextFactoryTests
{
    private const string VariableName = "ConnectionStrings__DefaultConnection";

    [Fact]
    public void CreateDbContext_EnvironmentConnectionString_ConfiguresNpgsqlAndMigrationsAssembly()
    {
        const string connectionString = "Host=factory-host;Port=5433;Database=factory-db;Username=factory-user;Password=factory-pass";
        var originalValue = Environment.GetEnvironmentVariable(VariableName);

        try
        {
            Environment.SetEnvironmentVariable(VariableName, connectionString);

            using var context = new DefaultContextFactory().CreateDbContext([]);

            context.Database.ProviderName.Should().Be("Npgsql.EntityFrameworkCore.PostgreSQL");
            context.Database.GetDbConnection().ConnectionString.Should().Be(connectionString);
            var migrationsAssembly = context.GetService<IDbContextOptions>()
                .Extensions.OfType<RelationalOptionsExtension>()
                .Single().MigrationsAssembly;
            migrationsAssembly.Should().Be(typeof(DefaultContext).Assembly.FullName);
        }
        finally
        {
            Environment.SetEnvironmentVariable(VariableName, originalValue);
        }
    }
}
