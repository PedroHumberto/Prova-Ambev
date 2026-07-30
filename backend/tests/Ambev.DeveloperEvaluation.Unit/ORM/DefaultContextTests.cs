using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Mapping;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.ORM;

public class DefaultContextTests
{
    [Fact]
    public async Task CommitAsync_DuplicateUserEmailConstraint_TranslatesException()
    {
        var postgresException = CreateUniqueViolation(UserConfiguration.UserEmailUniqueIndexName);
        var updateException = new DbUpdateException("Update failed.", postgresException);
        await using var context = new FailingDefaultContext(updateException);

        var action = () => context.CommitAsync(CancellationToken.None);

        var exception = await action.Should().ThrowAsync<DuplicateUserEmailException>();
        exception.Which.InnerException.Should().BeSameAs(updateException);
    }

    [Fact]
    public async Task CommitAsync_DifferentUniqueConstraint_DoesNotTranslateException()
    {
        var postgresException = CreateUniqueViolation("PK_Users");
        var updateException = new DbUpdateException("Update failed.", postgresException);
        await using var context = new FailingDefaultContext(updateException);

        var action = () => context.CommitAsync(CancellationToken.None);

        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        exception.Which.Should().BeSameAs(updateException);
    }

    private static PostgresException CreateUniqueViolation(string constraintName) => new(
        "duplicate key value violates unique constraint",
        "ERROR",
        "ERROR",
        PostgresErrorCodes.UniqueViolation,
        constraintName: constraintName);

    private sealed class FailingDefaultContext(DbUpdateException exception)
        : DefaultContext(new DbContextOptionsBuilder<DefaultContext>().Options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<int>(exception);
    }
}
