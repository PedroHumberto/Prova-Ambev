using Ambev.DeveloperEvaluation.Application.Users.DeleteUser;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application;

public class DeleteUserHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly DeleteUserHandler _handler;

    public DeleteUserHandlerTests()
    {
        _handler = new DeleteUserHandler(_userRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ExistingUser_DeletesAndCommitsOnce()
    {
        var command = new DeleteUserCommand(UserId);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        _userRepository.DeleteAsync(UserId, cancellationToken).Returns(true);

        var result = await _handler.Handle(command, cancellationToken);

        result.Success.Should().BeTrue();
        await _userRepository.Received(1).DeleteAsync(UserId, cancellationToken);
        await _unitOfWork.Received(1).CommitAsync(cancellationToken);
    }

    [Fact]
    public async Task Handle_UserNotFound_ThrowsAndDoesNotCommit()
    {
        var command = new DeleteUserCommand(UserId);
        _userRepository.DeleteAsync(UserId, Arg.Any<CancellationToken>()).Returns(false);

        var action = () => _handler.Handle(command, CancellationToken.None);

        await action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{UserId}*");
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RepositoryFails_PropagatesFailureAndDoesNotCommit()
    {
        var command = new DeleteUserCommand(UserId);
        var expectedException = new InvalidOperationException("Repository failed.");
        _userRepository.DeleteAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(expectedException));

        var action = () => _handler.Handle(command, CancellationToken.None);

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Should().BeSameAs(expectedException);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CommitFails_PropagatesFailure()
    {
        var command = new DeleteUserCommand(UserId);
        var expectedException = new InvalidOperationException("Commit failed.");
        _userRepository.DeleteAsync(UserId, Arg.Any<CancellationToken>()).Returns(true);
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(expectedException));

        var action = () => _handler.Handle(command, CancellationToken.None);

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Should().BeSameAs(expectedException);
        await _unitOfWork.Received(1).CommitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Handle_CommitCancellation_PropagatesCancellation()
    {
        var command = new DeleteUserCommand(UserId);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var cancellationToken = cancellationSource.Token;
        _userRepository.DeleteAsync(UserId, cancellationToken).Returns(true);
        _unitOfWork.CommitAsync(cancellationToken)
            .Returns(Task.FromCanceled<int>(cancellationToken));

        var action = () => _handler.Handle(command, cancellationToken);

        var exception = await action.Should().ThrowAsync<OperationCanceledException>();
        exception.Which.CancellationToken.Should().Be(cancellationToken);
        await _unitOfWork.Received(1).CommitAsync(cancellationToken);
    }
}
