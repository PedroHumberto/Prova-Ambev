using System.Net;
using Ambev.DeveloperEvaluation.Functional.Infrastructure;
using Ambev.DeveloperEvaluation.Functional.Support;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Sales;

[Collection(FunctionalApiCollection.Name)]
public sealed class SalesCrudApiTests(FunctionalApiFixture fixture)
{
    private static readonly Guid CustomerId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid BranchId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid ProductOneId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid ProductTwoId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    private readonly ApiTestClient _api = new(fixture.Client);

    [Fact]
    public async Task Sales_CompleteCrudAndIdempotentCancellation_PersistsObservableState()
    {
        var (_, token) = await _api.CreateActiveUserAndAuthenticateAsync(
            "sales-crud@example.com",
            "Sales CRUD");
        var created = await _api.CreateSaleAsync(
            token,
            "FUNC-CRUD-001",
            new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc),
            CustomerId,
            "CRUD Customer",
            BranchId,
            "CRUD Branch",
            new SaleItemInput(ProductOneId, "First Product", 4, 10.00m),
            new SaleItemInput(ProductTwoId, "Second Product", 2, 7.50m));
        var saleId = created.GetProperty("id").GetGuid();
        var firstItemId = created.GetProperty("items")[0].GetProperty("id").GetGuid();
        var secondItemId = created.GetProperty("items")[1].GetProperty("id").GetGuid();
        Assert.Equal(51.00m, created.GetProperty("totalAmount").GetDecimal());

        var (getResponse, getBody) = await _api.GetAsync($"/api/sales/{saleId}", token);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal("FUNC-CRUD-001", getBody.GetProperty("data").GetProperty("saleNumber").GetString());

        var (updateResponse, updateBody) = await _api.PutAsync($"/api/sales/{saleId}", new
        {
            saleNumber = "FUNC-CRUD-UPDATED",
            saleDate = new DateTime(2026, 1, 11, 12, 0, 0, DateTimeKind.Utc),
            customerId = CustomerId,
            customerName = "Updated Customer",
            branchId = BranchId,
            branchName = "Updated Branch",
            items = new[]
            {
                new { id = (Guid?)firstItemId, productId = ProductOneId, productName = "Updated Product", quantity = 10, unitPrice = 10.00m },
                new { id = (Guid?)null, productId = Guid.Parse("30000000-0000-0000-0000-000000000003"), productName = "Replacement Product", quantity = 1, unitPrice = 5.00m }
            }
        }, token);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = updateBody.GetProperty("data");
        Assert.Equal("FUNC-CRUD-UPDATED", updated.GetProperty("saleNumber").GetString());
        Assert.Equal(85.00m, updated.GetProperty("totalAmount").GetDecimal());
        var omittedItem = updated.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == secondItemId);
        Assert.Equal("Cancelled", omittedItem.GetProperty("status").GetString());

        var replacementItemId = firstItemId;
        foreach (var _ in Enumerable.Range(0, 2))
        {
            var (response, body) = await _api.DeleteAsync(
                $"/api/sales/{saleId}/items/{replacementItemId}",
                token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(body.GetProperty("success").GetBoolean());
        }

        foreach (var _ in Enumerable.Range(0, 2))
        {
            var (response, body) = await _api.DeleteAsync($"/api/sales/{saleId}", token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(body.GetProperty("success").GetBoolean());
        }

        var (_, finalBody) = await _api.GetAsync($"/api/sales/{saleId}", token);
        Assert.Equal("Cancelled", finalBody.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal(5m, finalBody.GetProperty("data").GetProperty("totalAmount").GetDecimal());
    }

    [Fact]
    public async Task Sales_WithoutBearerToken_ReturnsUnauthorizedProblemWithoutRabbitMq()
    {
        var (response, body) = await _api.GetAsync("/api/sales");

        ProblemDetailsAssertions.AssertProblem(
            response,
            body,
            HttpStatusCode.Unauthorized,
            "Unauthorized",
            "Authentication is required.",
            "/api/sales");
    }
}
