using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Enums;
using Ambev.DeveloperEvaluation.Integration.Infrastructure;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Users;

[Collection(PostgreSqlCollection.Name)]
public sealed class UserRepositoryTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task GetByIdAndEmailAsync_PersistedUser_ReturnsDetachedUser()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var user = CreateUser();
        await using (var writeContext = database.CreateContext())
        {
            await new UserRepository(writeContext).CreateAsync(user);
            await writeContext.CommitAsync();
        }

        await using var readContext = database.CreateContext();
        var repository = new UserRepository(readContext);

        var byId = await repository.GetByIdAsync(user.Id);
        var byEmail = await repository.GetByEmailAsync(user.Email);

        Assert.NotNull(byId);
        Assert.Equal(user.Id, byId.Id);
        Assert.Equal(user.Email, byId.Email);
        Assert.NotNull(byEmail);
        Assert.Equal(user.Id, byEmail.Id);
        Assert.Equal(user.Username, byEmail.Username);
        Assert.Empty(readContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task DeleteAsync_ExistingUser_MarksUserForDeletionAndPersistsRemoval()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        var user = CreateUser();
        await using (var setupContext = database.CreateContext())
        {
            await new UserRepository(setupContext).CreateAsync(user);
            await setupContext.CommitAsync();
        }

        await using (var deleteContext = database.CreateContext())
        {
            var deleted = await new UserRepository(deleteContext).DeleteAsync(user.Id);

            Assert.True(deleted);
            var deletedEntry = Assert.Single(deleteContext.ChangeTracker.Entries<User>());
            Assert.Equal(EntityState.Deleted, deletedEntry.State);
            await deleteContext.CommitAsync();
        }

        await using var verificationContext = database.CreateContext();
        Assert.False(await verificationContext.Users.AnyAsync(candidate => candidate.Id == user.Id));
    }

    [Fact]
    public async Task DeleteAsync_UnknownUser_ReturnsFalseWithoutTrackingChanges()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();

        var deleted = await new UserRepository(context).DeleteAsync(
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));

        Assert.False(deleted);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static User CreateUser() => new()
    {
        Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Username = "Repository User",
        Email = "repository.user@example.com",
        Phone = "+5511999999999",
        Password = "hashed-password",
        Role = UserRole.Customer,
        Status = UserStatus.Active,
        CreatedAt = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc)
    };
}
