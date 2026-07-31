using Ambev.DeveloperEvaluation.Application.Users.GetUser;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using AutoMapper;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application;

public sealed class GetUserHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IMapper _mapper = Substitute.For<IMapper>();
    private readonly GetUserHandler _handler;

    public GetUserHandlerTests()
    {
        _handler = new GetUserHandler(_userRepository, _mapper);
    }

    [Fact]
    public async Task Handle_UserExists_ReturnsMappedUserAndPropagatesCancellationToken()
    {
        var id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var command = new GetUserCommand(id);
        var user = new User { Id = id, Username = "Mapped User" };
        var expected = new GetUserResult { Id = id, Name = user.Username };
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        _userRepository.GetByIdAsync(id, cancellationToken).Returns(user);
        _mapper.Map<GetUserResult>(user).Returns(expected);

        var result = await _handler.Handle(command, cancellationToken);

        result.Should().BeSameAs(expected);
        await _userRepository.Received(1).GetByIdAsync(id, cancellationToken);
        _mapper.Received(1).Map<GetUserResult>(user);
    }

    [Fact]
    public async Task Handle_UserDoesNotExist_ThrowsKeyNotFoundWithoutMapping()
    {
        var id = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var command = new GetUserCommand(id);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        _userRepository.GetByIdAsync(id, cancellationToken).Returns((User?)null);

        var action = () => _handler.Handle(command, cancellationToken);

        await action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{id}*");
        await _userRepository.Received(1).GetByIdAsync(id, cancellationToken);
        _mapper.DidNotReceive().Map<GetUserResult>(Arg.Any<User>());
    }
}
