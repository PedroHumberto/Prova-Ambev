using Ambev.DeveloperEvaluation.Application.Users.CreateUser;
using Ambev.DeveloperEvaluation.Domain.Enums;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application;

public sealed class CreateUserCommandTests
{
    private readonly CreateUserCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ReturnsValidResultWithoutErrors()
    {
        var command = CreateValidCommand();

        var result = command.Validate();

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_AllFieldsInvalid_ReturnsInvalidResultWithValidationDetails()
    {
        var command = new CreateUserCommand
        {
            Email = "invalid-email",
            Username = "ab",
            Password = "weak",
            Phone = "invalid-phone",
            Status = UserStatus.Unknown,
            Role = UserRole.None
        };

        var result = command.Validate();

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().OnlyContain(error =>
            !string.IsNullOrWhiteSpace(error.Error)
            && !string.IsNullOrWhiteSpace(error.Detail));
    }

    [Fact]
    public void Validate_AllFieldsInvalid_ReportsEveryCommandProperty()
    {
        var command = new CreateUserCommand
        {
            Email = "invalid-email",
            Username = "ab",
            Password = "weak",
            Phone = "invalid-phone",
            Status = UserStatus.Unknown,
            Role = UserRole.None
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(candidate => candidate.Email);
        result.ShouldHaveValidationErrorFor(candidate => candidate.Username);
        result.ShouldHaveValidationErrorFor(candidate => candidate.Password);
        result.ShouldHaveValidationErrorFor(candidate => candidate.Phone);
        result.ShouldHaveValidationErrorFor(candidate => candidate.Status);
        result.ShouldHaveValidationErrorFor(candidate => candidate.Role);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(50)]
    public void Validate_UsernameAtSupportedBoundary_HasNoUsernameError(int length)
    {
        var command = CreateValidCommand();
        command.Username = new string('u', length);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(candidate => candidate.Username);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(51)]
    public void Validate_UsernameOutsideSupportedBoundary_HasUsernameError(int length)
    {
        var command = CreateValidCommand();
        command.Username = new string('u', length);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(candidate => candidate.Username);
    }

    private static CreateUserCommand CreateValidCommand() => new()
    {
        Email = "user@example.com",
        Username = "valid-user",
        Password = "StrongPassword1!",
        Phone = "+5511999999999",
        Status = UserStatus.Active,
        Role = UserRole.Customer
    };
}
