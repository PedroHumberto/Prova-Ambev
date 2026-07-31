using Ambev.DeveloperEvaluation.WebApi.Features.Auth.AuthenticateUserFeature;
using Ambev.DeveloperEvaluation.WebApi.Features.Users.DeleteUser;
using Ambev.DeveloperEvaluation.WebApi.Features.Users.GetUser;
using FluentValidation.TestHelper;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.WebApi;

public sealed class UserRequestValidatorTests
{
    [Fact]
    public void Validate_ValidAuthenticationRequest_HasNoErrors()
    {
        var request = new AuthenticateUserRequest
        {
            Email = "user@example.com",
            Password = "Valid1!Password"
        };

        var result = new AuthenticateUserRequestValidator().TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("", "Email is required")]
    [InlineData("not-an-email", "Invalid email format")]
    public void Validate_InvalidAuthenticationEmail_ReportsExpectedMessage(
        string email,
        string expectedMessage)
    {
        var request = new AuthenticateUserRequest
        {
            Email = email,
            Password = "Valid1!Password"
        };

        var result = new AuthenticateUserRequestValidator().TestValidate(request);

        result.ShouldHaveValidationErrorFor(candidate => candidate.Email)
            .WithErrorMessage(expectedMessage);
    }

    [Fact]
    public void Validate_EmptyAuthenticationPassword_ReportsRequiredMessage()
    {
        var request = new AuthenticateUserRequest
        {
            Email = "user@example.com",
            Password = string.Empty
        };

        var result = new AuthenticateUserRequestValidator().TestValidate(request);

        result.ShouldHaveValidationErrorFor(candidate => candidate.Password)
            .WithErrorMessage("Password is required");
    }

    [Fact]
    public void Validate_ValidUserIdentifiers_HaveNoErrors()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var getResult = new GetUserRequestValidator().TestValidate(new GetUserRequest { Id = id });
        var deleteResult = new DeleteUserRequestValidator().TestValidate(new DeleteUserRequest { Id = id });

        getResult.ShouldNotHaveAnyValidationErrors();
        deleteResult.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyUserIdentifiers_ReportRequiredMessage()
    {
        var getResult = new GetUserRequestValidator().TestValidate(new GetUserRequest());
        var deleteResult = new DeleteUserRequestValidator().TestValidate(new DeleteUserRequest());

        getResult.ShouldHaveValidationErrorFor(candidate => candidate.Id)
            .WithErrorMessage("User ID is required");
        deleteResult.ShouldHaveValidationErrorFor(candidate => candidate.Id)
            .WithErrorMessage("User ID is required");
    }
}
