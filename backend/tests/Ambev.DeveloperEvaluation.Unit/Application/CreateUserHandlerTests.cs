using Ambev.DeveloperEvaluation.Application.Common.Exceptions;
using Ambev.DeveloperEvaluation.Application.Users.CreateUser;
using Ambev.DeveloperEvaluation.Common.Security;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Unit.Domain;
using AutoMapper;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application;

public class CreateUserHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IMapper _mapper = Substitute.For<IMapper>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateUserHandler _handler;

    public CreateUserHandlerTests()
    {
        _handler = new CreateUserHandler(
            _userRepository,
            _mapper,
            _passwordHasher,
            _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesUserAndCommitsOnce()
    {
        var command = CreateUserHandlerTestData.GenerateValidCommand();
        var cancellationToken = new CancellationTokenSource().Token;
        var user = CreateUser(command);
        var expectedResult = new CreateUserResult { Id = user.Id };
        const string hashedPassword = "h@shedPassw0rd";

        _mapper.Map<User>(command).Returns(user);
        _passwordHasher.HashPassword(command.Password).Returns(hashedPassword);
        _userRepository.CreateAsync(user, cancellationToken).Returns(user);
        _mapper.Map<CreateUserResult>(user).Returns(expectedResult);

        var result = await _handler.Handle(command, cancellationToken);

        result.Should().BeSameAs(expectedResult);
        user.Password.Should().Be(hashedPassword);
        await _userRepository.Received(1).GetByEmailAsync(command.Email, cancellationToken);
        await _userRepository.Received(1).CreateAsync(user, cancellationToken);
        await _unitOfWork.Received(1).CommitAsync(cancellationToken);
    }

    [Fact]
    public async Task Handle_ExistingEmail_ThrowsAndDoesNotCreateOrCommit()
    {
        var command = CreateUserHandlerTestData.GenerateValidCommand();
        var existingUser = CreateUser(command);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        _userRepository.GetByEmailAsync(command.Email, cancellationToken).Returns(existingUser);

        var action = () => _handler.Handle(command, cancellationToken);

        await action.Should().ThrowAsync<UserAlreadyExistsException>()
            .WithMessage($"*{command.Email}*");
        await _userRepository.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RepositoryFails_PropagatesFailureAndDoesNotCommit()
    {
        var command = CreateUserHandlerTestData.GenerateValidCommand();
        var user = CreateUser(command);
        var expectedException = new InvalidOperationException("Repository failed.");
        _mapper.Map<User>(command).Returns(user);
        _passwordHasher.HashPassword(command.Password).Returns("hashedPassword");
        _userRepository.CreateAsync(user, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<User>(expectedException));

        var action = () => _handler.Handle(command, CancellationToken.None);

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Should().BeSameAs(expectedException);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RepositoryCancellation_PropagatesAndDoesNotCommit()
    {
        var command = CreateUserHandlerTestData.GenerateValidCommand();
        var user = CreateUser(command);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var cancellationToken = cancellationSource.Token;
        _mapper.Map<User>(command).Returns(user);
        _passwordHasher.HashPassword(command.Password).Returns("hashedPassword");
        _userRepository.CreateAsync(user, cancellationToken)
            .Returns(Task.FromCanceled<User>(cancellationToken));

        var action = () => _handler.Handle(command, cancellationToken);

        var exception = await action.Should().ThrowAsync<OperationCanceledException>();
        exception.Which.CancellationToken.Should().Be(cancellationToken);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CommitFails_PropagatesFailure()
    {
        var command = CreateUserHandlerTestData.GenerateValidCommand();
        var user = CreateUser(command);
        var expectedException = new InvalidOperationException("Commit failed.");
        _mapper.Map<User>(command).Returns(user);
        _passwordHasher.HashPassword(command.Password).Returns("hashedPassword");
        _userRepository.CreateAsync(user, Arg.Any<CancellationToken>()).Returns(user);
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(expectedException));

        var action = () => _handler.Handle(command, CancellationToken.None);

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Should().BeSameAs(expectedException);
        await _unitOfWork.Received(1).CommitAsync(CancellationToken.None);
    }

    private static User CreateUser(CreateUserCommand command) => new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Username = command.Username,
        Password = command.Password,
        Email = command.Email,
        Phone = command.Phone,
        Status = command.Status,
        Role = command.Role
    };
}
