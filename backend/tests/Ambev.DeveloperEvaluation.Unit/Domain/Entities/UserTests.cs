using Ambev.DeveloperEvaluation.Common.Security;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Enums;
using Ambev.DeveloperEvaluation.Unit.Domain.Entities.TestData;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Entities;

/// <summary>
/// Contains unit tests for the User entity class.
/// Tests cover status changes and validation scenarios.
/// </summary>
public class UserTests
{
    /// <summary>
    /// Tests that when a suspended user is activated, their status changes to Active.
    /// </summary>
    [Fact(DisplayName = "User status should change to Active when activated")]
    public void Given_SuspendedUser_When_Activated_Then_StatusShouldBeActive()
    {
        // Arrange
        var user = UserTestData.GenerateValidUser();
        user.Status = UserStatus.Suspended;

        // Act
        user.Activate();

        // Assert
        Assert.Equal(UserStatus.Active, user.Status);
    }

    /// <summary>
    /// Tests that when an active user is suspended, their status changes to Suspended.
    /// </summary>
    [Fact(DisplayName = "User status should change to Suspended when suspended")]
    public void Given_ActiveUser_When_Suspended_Then_StatusShouldBeSuspended()
    {
        // Arrange
        var user = UserTestData.GenerateValidUser();
        user.Status = UserStatus.Active;

        // Act
        user.Suspend();

        // Assert
        Assert.Equal(UserStatus.Suspended, user.Status);
        Assert.NotNull(user.UpdatedAt);
    }

    [Fact]
    public void Deactivate_ActiveUser_ChangesStatusAndRecordsUpdateTime()
    {
        // Arrange
        var previousUpdate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var user = new User
        {
            Status = UserStatus.Active,
            UpdatedAt = previousUpdate
        };

        // Act
        user.Deactivate();

        // Assert
        Assert.Equal(UserStatus.Inactive, user.Status);
        Assert.NotNull(user.UpdatedAt);
        Assert.NotEqual(previousUpdate, user.UpdatedAt.Value);
        Assert.Equal(DateTimeKind.Utc, user.UpdatedAt.Value.Kind);
    }

    [Fact]
    public void IUserProjection_PopulatedUser_ExposesIdentityNameAndRoleAsSecurityStrings()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Username = "domain.user",
            Role = UserRole.Admin
        };

        // Act
        var securityUser = (IUser)user;

        // Assert
        Assert.Equal(user.Id.ToString(), securityUser.Id);
        Assert.Equal("domain.user", securityUser.Username);
        Assert.Equal("Admin", securityUser.Role);
    }

    /// <summary>
    /// Tests that validation passes when all user properties are valid.
    /// </summary>
    [Fact(DisplayName = "Validation should pass for valid user data")]
    public void Given_ValidUserData_When_Validated_Then_ShouldReturnValid()
    {
        // Arrange
        var user = UserTestData.GenerateValidUser();

        // Act
        var result = user.Validate();

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// Tests that validation fails when user properties are invalid.
    /// </summary>
    [Fact(DisplayName = "Validation should fail for invalid user data")]
    public void Given_InvalidUserData_When_Validated_Then_ShouldReturnInvalid()
    {
        // Arrange
        var user = new User
        {
            Username = "", // Invalid: empty
            Password = UserTestData.GenerateInvalidPassword(), // Invalid: doesn't meet password requirements
            Email = UserTestData.GenerateInvalidEmail(), // Invalid: not a valid email
            Phone = UserTestData.GenerateInvalidPhone(), // Invalid: doesn't match pattern
            Status = UserStatus.Unknown, // Invalid: cannot be Unknown
            Role = UserRole.None // Invalid: cannot be None
        };

        // Act
        var result = user.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }
}
