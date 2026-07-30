using Ambev.DeveloperEvaluation.Application;
using Ambev.DeveloperEvaluation.Application.Users.CreateUser;
using Ambev.DeveloperEvaluation.Common.Validation;
using Ambev.DeveloperEvaluation.WebApi;
using Ambev.DeveloperEvaluation.WebApi.Features.Users.CreateUser;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application;

public class ServiceRegistrationTests
{
    [Fact]
    public void ValidatorsAndPipeline_ProgramAssemblies_AreResolvable()
    {
        var services = new ServiceCollection();
        services.AddValidatorsFromAssembly(typeof(ApplicationLayer).Assembly);
        services.AddValidatorsFromAssembly(typeof(Program).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        using var provider = services.BuildServiceProvider();

        provider.GetServices<IValidator<CreateUserCommand>>()
            .Should().ContainSingle(validator => validator is CreateUserCommandValidator);
        provider.GetServices<IValidator<CreateUserRequest>>()
            .Should().ContainSingle(validator => validator is CreateUserRequestValidator);
        provider.GetServices<IPipelineBehavior<CreateUserCommand, CreateUserResult>>()
            .Should().ContainSingle(behavior => behavior is ValidationBehavior<CreateUserCommand, CreateUserResult>);
    }
}
