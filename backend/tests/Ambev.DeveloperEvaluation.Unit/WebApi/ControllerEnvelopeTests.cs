using Ambev.DeveloperEvaluation.Application.Auth.AuthenticateUser;
using Ambev.DeveloperEvaluation.Application.Users.DeleteUser;
using Ambev.DeveloperEvaluation.Application.Users.GetUser;
using Ambev.DeveloperEvaluation.WebApi.Common;
using Ambev.DeveloperEvaluation.WebApi.Features.Auth;
using Ambev.DeveloperEvaluation.WebApi.Features.Auth.AuthenticateUserFeature;
using Ambev.DeveloperEvaluation.WebApi.Features.Users;
using Ambev.DeveloperEvaluation.WebApi.Features.Users.DeleteUser;
using Ambev.DeveloperEvaluation.WebApi.Features.Users.GetUser;
using AutoMapper;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.WebApi;

public class ControllerEnvelopeTests
{
    [Fact]
    public async Task GetUser_Success_ReturnsSingleEnvelopeContainingResponseDto()
    {
        var mediator = Substitute.For<IMediator>();
        var mapper = Substitute.For<IMapper>();
        var controller = new UsersController(mediator, mapper);
        var request = new GetUserRequest { Id = Guid.Parse("11111111-1111-1111-1111-111111111111") };
        var command = new GetUserCommand(request.Id);
        var applicationResponse = new GetUserResult { Id = request.Id, Name = "Test User" };
        var responseDto = new GetUserResponse { Id = request.Id, Name = "Test User" };
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        mapper.Map<GetUserCommand>(request.Id).Returns(command);
        mediator.Send(command, cancellationToken).Returns(applicationResponse);
        mapper.Map<GetUserResponse>(applicationResponse).Returns(responseDto);

        var actionResult = await controller.GetUser(request, cancellationToken);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var envelope = okResult.Value.Should().BeOfType<ApiResponseWithData<GetUserResponse>>().Subject;
        envelope.Success.Should().BeTrue();
        envelope.Message.Should().Be("User retrieved successfully");
        envelope.Data.Should().BeSameAs(responseDto);
        envelope.Data.Should().NotBeAssignableTo<ApiResponse>();
    }

    [Fact]
    public async Task AuthenticateUser_Success_ReturnsSingleEnvelopeContainingResponseDto()
    {
        var mediator = Substitute.For<IMediator>();
        var mapper = Substitute.For<IMapper>();
        var controller = new AuthController(mediator, mapper);
        var request = new AuthenticateUserRequest
        {
            Email = "test.user@example.com",
            Password = "Valid1!Password"
        };
        var command = new AuthenticateUserCommand { Email = request.Email, Password = request.Password };
        var applicationResponse = new AuthenticateUserResult { Token = "jwt-token" };
        var responseDto = new AuthenticateUserResponse { Token = "jwt-token" };
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        mapper.Map<AuthenticateUserCommand>(request).Returns(command);
        mediator.Send(command, cancellationToken).Returns(applicationResponse);
        mapper.Map<AuthenticateUserResponse>(applicationResponse).Returns(responseDto);

        var actionResult = await controller.AuthenticateUser(request, cancellationToken);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var envelope = okResult.Value.Should().BeOfType<ApiResponseWithData<AuthenticateUserResponse>>().Subject;
        envelope.Success.Should().BeTrue();
        envelope.Message.Should().Be("User authenticated successfully");
        envelope.Data.Should().BeSameAs(responseDto);
        envelope.Data.Should().NotBeAssignableTo<ApiResponse>();
    }

    [Fact]
    public async Task DeleteUser_Success_ReturnsSingleNonGenericEnvelope()
    {
        var mediator = Substitute.For<IMediator>();
        var mapper = Substitute.For<IMapper>();
        var controller = new UsersController(mediator, mapper);
        var request = new DeleteUserRequest { Id = Guid.Parse("22222222-2222-2222-2222-222222222222") };
        var command = new DeleteUserCommand(request.Id);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        mapper.Map<DeleteUserCommand>(request.Id).Returns(command);
        mediator.Send(command, cancellationToken).Returns(new DeleteUserResponse { Success = true });

        var actionResult = await controller.DeleteUser(request, cancellationToken);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var envelope = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        envelope.Success.Should().BeTrue();
        envelope.Message.Should().Be("User deleted successfully");
        okResult.Value.Should().NotBeAssignableTo<ApiResponseWithData<ApiResponse>>();
    }
}
