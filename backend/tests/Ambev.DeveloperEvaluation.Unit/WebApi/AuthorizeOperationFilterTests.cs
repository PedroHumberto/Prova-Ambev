using System.Reflection;
using Ambev.DeveloperEvaluation.WebApi.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using NSubstitute;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.WebApi;

public sealed class AuthorizeOperationFilterTests
{
    [Fact]
    public void Apply_EndpointWithoutAuthorize_DoesNotAddSecurityRequirement()
    {
        var operation = new OpenApiOperation();

        Apply(operation, typeof(UnsecuredController).GetMethod(nameof(UnsecuredController.Action))!);

        Assert.Empty(operation.Security);
    }

    [Fact]
    public void Apply_AllowAnonymousEndpointOnAuthorizedController_DoesNotAddSecurityRequirement()
    {
        var operation = new OpenApiOperation();

        Apply(operation, typeof(AuthorizedController).GetMethod(nameof(AuthorizedController.AnonymousAction))!);

        Assert.Empty(operation.Security);
    }

    [Theory]
    [InlineData(typeof(UnsecuredController), nameof(UnsecuredController.AuthorizedAction))]
    [InlineData(typeof(AuthorizedController), nameof(AuthorizedController.Action))]
    public void Apply_AuthorizedEndpoint_AddsBearerSecurityRequirement(Type controllerType, string methodName)
    {
        var operation = new OpenApiOperation();

        Apply(operation, controllerType.GetMethod(methodName)!);

        var requirement = Assert.Single(operation.Security!);
        var scheme = Assert.Single(requirement.Keys);
        Assert.Equal(ReferenceType.SecurityScheme, scheme.Reference.Type);
        Assert.Equal("Bearer", scheme.Reference.Id);
        Assert.Empty(requirement[scheme]);
    }

    [Fact]
    public void Apply_AuthorizedEndpointWithExistingSecurity_PreservesAndAppendsRequirement()
    {
        var existingRequirement = new OpenApiSecurityRequirement();
        var operation = new OpenApiOperation
        {
            Security = [existingRequirement]
        };

        Apply(operation, typeof(AuthorizedController).GetMethod(nameof(AuthorizedController.Action))!);

        Assert.Equal(2, operation.Security.Count);
        Assert.Same(existingRequirement, operation.Security[0]);
        Assert.Equal("Bearer", Assert.Single(operation.Security[1].Keys).Reference.Id);
    }

    private static void Apply(OpenApiOperation operation, MethodInfo methodInfo)
    {
        var context = new OperationFilterContext(
            new ApiDescription(),
            Substitute.For<ISchemaGenerator>(),
            new SchemaRepository(),
            methodInfo);

        new AuthorizeOperationFilter().Apply(operation, context);
    }

    private sealed class UnsecuredController
    {
        public void Action()
        {
        }

        [Authorize]
        public void AuthorizedAction()
        {
        }
    }

    [Authorize]
    private sealed class AuthorizedController
    {
        public void Action()
        {
        }

        [AllowAnonymous]
        public void AnonymousAction()
        {
        }
    }
}
