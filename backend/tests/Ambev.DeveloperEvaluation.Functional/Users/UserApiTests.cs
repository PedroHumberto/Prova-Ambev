using System.Net;
using Ambev.DeveloperEvaluation.Functional.Infrastructure;
using Ambev.DeveloperEvaluation.Functional.Support;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Users;

[Collection(FunctionalApiCollection.Name)]
public sealed class UserApiTests(FunctionalApiFixture fixture)
{
    private readonly ApiTestClient _api = new(fixture.Client);

    [Fact]
    public async Task Users_CreateGetDeleteThenGet_ReturnsCurrentEnvelopeContractsAndNotFound()
    {
        var (id, _) = await _api.CreateActiveUserAndAuthenticateAsync(
            "users-crud@example.com",
            "Users CRUD");

        var (getResponse, getBody) = await _api.GetAsync($"/api/Users/{id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.True(getBody.GetProperty("success").GetBoolean());
        Assert.Equal(id, getBody.GetProperty("data").GetProperty("id").GetGuid());
        Assert.Equal("users-crud@example.com", getBody.GetProperty("data").GetProperty("email").GetString());

        var (deleteResponse, deleteBody) = await _api.DeleteAsync($"/api/Users/{id}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.True(deleteBody.GetProperty("success").GetBoolean());

        var (missingResponse, missingBody) = await _api.GetAsync($"/api/Users/{id}");
        ProblemDetailsAssertions.AssertProblem(
            missingResponse,
            missingBody,
            HttpStatusCode.NotFound,
            "Not Found",
            $"User with ID {id} not found",
            $"/api/Users/{id}");
    }

    [Fact]
    public async Task Authenticate_ValidAndInvalidCredentials_ReturnsRealJwtThenUnauthorizedProblem()
    {
        var (_, token) = await _api.CreateActiveUserAndAuthenticateAsync(
            "users-auth@example.com",
            "Users Auth");
        Assert.True(token.Split('.').Length == 3);

        var (response, body) = await _api.PostAsync("/api/Auth", new
        {
            email = "users-auth@example.com",
            password = "WrongPassword1!"
        });

        ProblemDetailsAssertions.AssertProblem(
            response,
            body,
            HttpStatusCode.Unauthorized,
            "Unauthorized",
            "Authentication failed.",
            "/api/Auth");
    }
}
