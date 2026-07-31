using Ambev.DeveloperEvaluation.Application.Auth.AuthenticateUser;
using Ambev.DeveloperEvaluation.Common.Security;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Enums;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application;

public sealed class AuthenticateUserHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenGenerator _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly AuthenticateUserHandler _handler;

    public AuthenticateUserHandlerTests()
    {
        _handler = new AuthenticateUserHandler(
            _userRepository,
            _passwordHasher,
            _jwtTokenGenerator);
    }

    [Fact]
    public async Task Handle_ActiveUserWithValidPassword_ReturnsIdentityAndGeneratedToken()
    {
        var command = new AuthenticateUserCommand
        {
            Email = "active.user@example.com",
            Password = "PlainPassword1!"
        };
        var user = CreateUser(UserStatus.Active);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        _userRepository.GetByEmailAsync(command.Email, cancellationToken).Returns(user);
        _passwordHasher.VerifyPassword(command.Password, user.Password).Returns(true);
        _jwtTokenGenerator.GenerateToken(user).Returns("signed-jwt-token");

        var result = await _handler.Handle(command, cancellationToken);

        result.Token.Should().Be("signed-jwt-token");
        result.Email.Should().Be(user.Email);
        result.Name.Should().Be(user.Username);
        result.Role.Should().Be(nameof(UserRole.Manager));
        await _userRepository.Received(1).GetByEmailAsync(command.Email, cancellationToken);
        _jwtTokenGenerator.Received(1).GenerateToken(user);
    }

    [Fact]
    public async Task Handle_UserDoesNotExist_ThrowsUnauthorizedWithoutVerifyingOrGeneratingToken()
    {
        var command = new AuthenticateUserCommand
        {
            Email = "missing.user@example.com",
            Password = "PlainPassword1!"
        };
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        _userRepository.GetByEmailAsync(command.Email, cancellationToken).Returns((User?)null);

        var action = () => _handler.Handle(command, cancellationToken);

        await action.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid credentials");
        _passwordHasher.DidNotReceive().VerifyPassword(Arg.Any<string>(), Arg.Any<string>());
        _jwtTokenGenerator.DidNotReceive().GenerateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_PasswordDoesNotMatch_ThrowsUnauthorizedWithoutGeneratingToken()
    {
        var command = new AuthenticateUserCommand
        {
            Email = "active.user@example.com",
            Password = "WrongPassword1!"
        };
        var user = CreateUser(UserStatus.Active);
        _userRepository.GetByEmailAsync(command.Email, CancellationToken.None).Returns(user);
        _passwordHasher.VerifyPassword(command.Password, user.Password).Returns(false);

        var action = () => _handler.Handle(command, CancellationToken.None);

        await action.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid credentials");
        _jwtTokenGenerator.DidNotReceive().GenerateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_InactiveUser_ThrowsUnauthorizedWithoutGeneratingToken()
    {
        var command = new AuthenticateUserCommand
        {
            Email = "inactive.user@example.com",
            Password = "PlainPassword1!"
        };
        var user = CreateUser(UserStatus.Inactive);
        _userRepository.GetByEmailAsync(command.Email, CancellationToken.None).Returns(user);
        _passwordHasher.VerifyPassword(command.Password, user.Password).Returns(true);

        var action = () => _handler.Handle(command, CancellationToken.None);

        await action.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User is not active");
        _jwtTokenGenerator.DidNotReceive().GenerateToken(Arg.Any<User>());
    }

    private static User CreateUser(UserStatus status) => new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Username = "Active User",
        Email = "active.user@example.com",
        Phone = "+5511999999999",
        Password = "stored-password-hash",
        Role = UserRole.Manager,
        Status = status
    };
}
