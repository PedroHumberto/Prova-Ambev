using Ambev.DeveloperEvaluation.Application.Users.CreateUser;
using Ambev.DeveloperEvaluation.Common.Validation;
using Ambev.DeveloperEvaluation.Domain.Enums;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_InvalidRequest_ThrowsValidationExceptionWithoutInvokingNext()
    {
        var behavior = new ValidationBehavior<CreateUserCommand, CreateUserResult>(
            [new CreateUserCommandValidator()]);
        var nextInvoked = false;
        RequestHandlerDelegate<CreateUserResult> next = () =>
        {
            nextInvoked = true;
            return Task.FromResult(new CreateUserResult());
        };

        var act = () => behavior.Handle(new CreateUserCommand(), next, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().Contain(error => error.PropertyName == nameof(CreateUserCommand.Username));
        nextInvoked.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidRequest_InvokesNextAndReturnsItsResult()
    {
        var behavior = new ValidationBehavior<CreateUserCommand, CreateUserResult>(
            [new CreateUserCommandValidator()]);
        var command = new CreateUserCommand
        {
            Username = "test.user",
            Password = "Valid1!Password",
            Email = "test.user@example.com",
            Phone = "+5511999999999",
            Status = UserStatus.Active,
            Role = UserRole.Customer
        };
        var expected = new CreateUserResult { Id = Guid.NewGuid() };
        RequestHandlerDelegate<CreateUserResult> next = () => Task.FromResult(expected);

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.Should().BeSameAs(expected);
    }
}
