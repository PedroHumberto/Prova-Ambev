using System.Net;
using Ambev.DeveloperEvaluation.Functional.Infrastructure;
using Ambev.DeveloperEvaluation.Functional.Support;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Sales;

[Collection(FunctionalApiCollection.Name)]
public sealed class SalesProblemDetailsApiTests(FunctionalApiFixture fixture)
{
    private static readonly Guid CustomerId = Guid.Parse("11000000-0000-0000-0000-000000000001");
    private static readonly Guid BranchId = Guid.Parse("21000000-0000-0000-0000-000000000001");
    private static readonly Guid ProductId = Guid.Parse("31000000-0000-0000-0000-000000000001");
    private readonly ApiTestClient _api = new(fixture.Client);

    [Fact]
    public async Task Sales_ExpectedFailures_ReturnProblemDetailsFor400404409And422()
    {
        var (_, token) = await _api.CreateActiveUserAndAuthenticateAsync(
            "sales-problems@example.com",
            "Sales Problems");

        var (badRequest, badRequestBody) = await _api.GetAsync("/api/sales?unknown=value", token);
        ProblemDetailsAssertions.AssertProblem(
            badRequest,
            badRequestBody,
            HttpStatusCode.BadRequest,
            "Validation failed",
            "One or more validation errors occurred.",
            "/api/sales");
        Assert.True(badRequestBody.GetProperty("errors").TryGetProperty("unknown", out _));

        var missingId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var (notFound, notFoundBody) = await _api.GetAsync($"/api/sales/{missingId}", token);
        ProblemDetailsAssertions.AssertProblem(
            notFound,
            notFoundBody,
            HttpStatusCode.NotFound,
            "Not Found",
            $"Sale with ID {missingId} not found.",
            $"/api/sales/{missingId}");

        await _api.CreateSaleAsync(
            token,
            "FUNC-PROBLEM-DUPLICATE",
            new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc),
            CustomerId,
            "Problem Customer",
            BranchId,
            "Problem Branch",
            new SaleItemInput(ProductId, "Problem Product", 2, 10m));
        var duplicatePayload = CreatePayload(
            "func-problem-duplicate",
            new[] { new SaleItemInput(Guid.Parse("31000000-0000-0000-0000-000000000002"), "Other Product", 2, 10m) });
        var (conflict, conflictBody) = await _api.PostAsync("/api/sales", duplicatePayload, token);
        ProblemDetailsAssertions.AssertProblem(
            conflict,
            conflictBody,
            HttpStatusCode.Conflict,
            "Conflict",
            "A sale with number 'func-problem-duplicate' already exists.",
            "/api/sales");

        var invalidDomainPayload = CreatePayload(
            "FUNC-PROBLEM-DOMAIN",
            new[]
            {
                new SaleItemInput(ProductId, "Duplicate Product", 2, 10m),
                new SaleItemInput(ProductId, "Duplicate Product", 3, 10m)
            });
        var (unprocessable, unprocessableBody) = await _api.PostAsync("/api/sales", invalidDomainPayload, token);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unprocessable.StatusCode);
        Assert.Equal("application/problem+json", unprocessable.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Unprocessable Entity", unprocessableBody.GetProperty("title").GetString());
        Assert.Equal(422, unprocessableBody.GetProperty("status").GetInt32());
        Assert.Equal(
            $"An active item for product '{ProductId}' already exists in the sale.",
            unprocessableBody.GetProperty("detail").GetString());
        Assert.Equal("/api/sales", unprocessableBody.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task Sales_InvalidOrder_ReturnsValidationProblemWithErrors()
    {
        var (_, token) = await _api.CreateActiveUserAndAuthenticateAsync(
            "sales-order-error@example.com",
            "Sales Order Error");

        var (response, body) = await _api.GetAsync("/api/sales?_order=notAField%20asc", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("title").GetString()));
        Assert.True(body.TryGetProperty("errors", out var errors));
        Assert.True(errors.EnumerateObject().Any());
    }

    private static object CreatePayload(string saleNumber, SaleItemInput[] items) => new
    {
        saleNumber,
        saleDate = new DateTime(2026, 2, 2, 12, 0, 0, DateTimeKind.Utc),
        customerId = CustomerId,
        customerName = "Problem Customer",
        branchId = BranchId,
        branchName = "Problem Branch",
        items
    };
}
