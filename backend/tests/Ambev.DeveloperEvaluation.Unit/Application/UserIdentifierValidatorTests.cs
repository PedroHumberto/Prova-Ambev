using Ambev.DeveloperEvaluation.Application.Users.DeleteUser;
using Ambev.DeveloperEvaluation.Application.Users.GetUser;
using FluentValidation.TestHelper;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application;

public sealed class UserIdentifierValidatorTests
{
    private static readonly Guid ValidUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Validate_ValidDeleteUserId_HasNoErrors()
    {
        var result = new DeleteUserValidator().TestValidate(new DeleteUserCommand(ValidUserId));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyDeleteUserId_HasRequiredIdError()
    {
        var result = new DeleteUserValidator().TestValidate(new DeleteUserCommand(Guid.Empty));

        result.ShouldHaveValidationErrorFor(command => command.Id)
            .WithErrorMessage("User ID is required");
    }

    [Fact]
    public void Validate_ValidGetUserId_HasNoErrors()
    {
        var result = new GetUserValidator().TestValidate(new GetUserCommand(ValidUserId));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyGetUserId_HasRequiredIdError()
    {
        var result = new GetUserValidator().TestValidate(new GetUserCommand(Guid.Empty));

        result.ShouldHaveValidationErrorFor(command => command.Id)
            .WithErrorMessage("User ID is required");
    }
}
