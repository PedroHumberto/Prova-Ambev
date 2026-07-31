using System.Net;
using System.Text.Json;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Support;

internal static class ProblemDetailsAssertions
{
    public static void AssertProblem(
        HttpResponseMessage response,
        JsonElement body,
        HttpStatusCode status,
        string title,
        string detail,
        string instance)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.Equal(title, body.GetProperty("title").GetString());
        Assert.Equal(detail, body.GetProperty("detail").GetString());
        Assert.Equal(instance, body.GetProperty("instance").GetString());
    }
}
