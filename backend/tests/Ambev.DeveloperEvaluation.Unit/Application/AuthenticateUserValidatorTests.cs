using Ambev.DeveloperEvaluation.Application.Auth.AuthenticateUser;
using FluentValidation.TestHelper;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application;

public sealed class AuthenticateUserValidatorTests
{
    private readonly AuthenticateUserValidator _validator = new();

    [Fact]
    public void Validate_ValidCredentials_HasNoErrors()
    {
        var command = new AuthenticateUserCommand
        {
            Email = "user@example.com",
            Password = "secret"
        };

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Validate_InvalidEmail_HasEmailError(string email)
    {
        var command = new AuthenticateUserCommand
        {
            Email = email,
            Password = "secret"
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(candidate => candidate.Email);
        result.ShouldNotHaveValidationErrorFor(candidate => candidate.Password);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    public void Validate_MissingOrShortPassword_HasPasswordError(string password)
    {
        var command = new AuthenticateUserCommand
        {
            Email = "user@example.com",
            Password = password
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(candidate => candidate.Password);
        result.ShouldNotHaveValidationErrorFor(candidate => candidate.Email);
    }
}
