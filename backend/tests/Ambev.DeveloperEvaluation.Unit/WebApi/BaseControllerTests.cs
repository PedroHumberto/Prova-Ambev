using System.Security.Claims;
using Ambev.DeveloperEvaluation.WebApi.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.WebApi;

public sealed class BaseControllerTests
{
    [Fact]
    public void GetCurrentUserId_NameIdentifierClaim_ReturnsParsedIdentifier()
    {
        var controller = CreateController(new Claim(ClaimTypes.NameIdentifier, "42"));

        var userId = controller.CurrentUserId();

        Assert.Equal(42, userId);
    }

    [Fact]
    public void GetCurrentUserEmail_EmailClaim_ReturnsEmail()
    {
        var controller = CreateController(new Claim(ClaimTypes.Email, "user@example.com"));

        var email = controller.CurrentUserEmail();

        Assert.Equal("user@example.com", email);
    }

    [Fact]
    public void GetCurrentUserId_MissingClaim_ThrowsNullReferenceException()
    {
        var controller = CreateController();

        Assert.Throws<NullReferenceException>(() => controller.CurrentUserId());
    }

    [Fact]
    public void GetCurrentUserId_NonNumericClaim_ThrowsFormatException()
    {
        var controller = CreateController(new Claim(ClaimTypes.NameIdentifier, "not-numeric"));

        Assert.Throws<FormatException>(() => controller.CurrentUserId());
    }

    [Fact]
    public void GetCurrentUserEmail_MissingClaim_ThrowsNullReferenceException()
    {
        var controller = CreateController();

        Assert.Throws<NullReferenceException>(() => controller.CurrentUserEmail());
    }

    [Fact]
    public void Ok_Data_WrapsSuccessfulResponse()
    {
        var controller = CreateController();
        var data = new TestResponse("value");

        var result = Assert.IsType<OkObjectResult>(controller.Success(data));

        var response = Assert.IsType<ApiResponseWithData<TestResponse>>(result.Value);
        Assert.True(response.Success);
        Assert.Same(data, response.Data);
    }

    [Fact]
    public void Created_RouteAndData_WrapsSuccessfulResponse()
    {
        var controller = CreateController();
        var routeValues = new { id = 42 };
        var data = new TestResponse("created");

        var result = Assert.IsType<CreatedAtRouteResult>(controller.CreatedResponse("GetById", routeValues, data));

        Assert.Equal("GetById", result.RouteName);
        Assert.Equal(42, result.RouteValues!["id"]);
        var response = Assert.IsType<ApiResponseWithData<TestResponse>>(result.Value);
        Assert.True(response.Success);
        Assert.Same(data, response.Data);
    }

    [Fact]
    public void BadRequest_Message_WrapsFailedResponse()
    {
        var result = Assert.IsType<BadRequestObjectResult>(CreateController().Invalid("invalid request"));

        var response = Assert.IsType<ApiResponse>(result.Value);
        Assert.False(response.Success);
        Assert.Equal("invalid request", response.Message);
    }

    [Fact]
    public void NotFound_WithoutMessage_UsesDefaultFailedResponse()
    {
        var result = Assert.IsType<NotFoundObjectResult>(CreateController().Missing());

        var response = Assert.IsType<ApiResponse>(result.Value);
        Assert.False(response.Success);
        Assert.Equal("Resource not found", response.Message);
    }

    private static TestController CreateController(params Claim[] claims) =>
        new()
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                }
            }
        };

    private sealed record TestResponse(string Value);

    private sealed class TestController : BaseController
    {
        public int CurrentUserId() => GetCurrentUserId();

        public string CurrentUserEmail() => GetCurrentUserEmail();

        public IActionResult Success<T>(T data) => Ok(data);

        public IActionResult CreatedResponse<T>(string routeName, object routeValues, T data) =>
            Created(routeName, routeValues, data);

        public IActionResult Invalid(string message) => BadRequest(message);

        public IActionResult Missing() => NotFound();
    }
}
