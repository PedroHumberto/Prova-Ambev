using System.Net;
using System.Text.Json;
using Ambev.DeveloperEvaluation.Functional.Infrastructure;
using Ambev.DeveloperEvaluation.Functional.Support;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Sales;

[Collection(FunctionalApiCollection.Name)]
public sealed class SalesQueryApiTests(FunctionalApiFixture fixture)
{
    private static readonly Guid MatchingCustomerId = Guid.Parse("12000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherCustomerId = Guid.Parse("12000000-0000-0000-0000-000000000002");
    private static readonly Guid PagingCustomerId = Guid.Parse("12000000-0000-0000-0000-000000000003");
    private static readonly Guid MatchingBranchId = Guid.Parse("22000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherBranchId = Guid.Parse("22000000-0000-0000-0000-000000000002");
    private readonly ApiTestClient _api = new(fixture.Client);

    [Fact]
    public async Task List_TextUuidUtcAndStatusFilters_ReturnOnlyControlledMatchingSale()
    {
        var token = await SeedSalesAsync();
        var cancelled = await _api.CreateSaleAsync(
            token,
            "FUNC-QUERY-CANCELLED",
            new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc),
            MatchingCustomerId,
            "Needle Customer",
            MatchingBranchId,
            "Needle Branch",
            Item(9));
        var cancelledId = cancelled.GetProperty("id").GetGuid();
        await _api.DeleteAsync($"/api/sales/{cancelledId}", token);

        var query = "/api/sales?saleNumber=FUNC-QUERY-CANCELLED"
            + $"&customerId={MatchingCustomerId:D}"
            + "&customerName=Needle%20Customer"
            + $"&branchId={MatchingBranchId:D}"
            + "&branchName=Needle%20Branch"
            + "&saleDateFrom=2026-03-15T00%3A00%3A00Z"
            + "&saleDateTo=2026-03-16T00%3A00%3A00Z"
            + "&status=Cancelled";
        var (response, body) = await _api.GetAsync(query, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = body.GetProperty("data");
        Assert.Equal(1, data.GetProperty("totalCount").GetInt32());
        var item = Assert.Single(data.GetProperty("items").EnumerateArray());
        Assert.Equal(cancelledId, item.GetProperty("id").GetGuid());
        Assert.Equal("Cancelled", item.GetProperty("status").GetString());
    }

    [Fact]
    public async Task List_PaginationAndAscDescCompositeOrdering_ReturnDistinctDeterministicPages()
    {
        var token = await SeedPagingSalesAsync();
        var filter = $"customerId={PagingCustomerId:D}";

        var (_, firstPageBody) = await _api.GetAsync(
            $"/api/sales?{filter}&_page=1&_size=2&_order=branchName%20asc%2CsaleNumber%20desc",
            token);
        var (_, secondPageBody) = await _api.GetAsync(
            $"/api/sales?{filter}&_page=2&_size=2&_order=branchName%20asc%2CsaleNumber%20desc",
            token);
        var firstData = firstPageBody.GetProperty("data");
        var secondData = secondPageBody.GetProperty("data");
        Assert.Equal(4, firstData.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, firstData.GetProperty("totalPages").GetInt32());
        Assert.Equal(1, firstData.GetProperty("pageNumber").GetInt32());
        Assert.Equal(2, secondData.GetProperty("pageNumber").GetInt32());
        Assert.Equal(new[] { "FUNC-PAGE-002", "FUNC-PAGE-001" }, SaleNumbers(firstData));
        Assert.Equal(new[] { "FUNC-PAGE-004", "FUNC-PAGE-003" }, SaleNumbers(secondData));

        var (_, descendingBody) = await _api.GetAsync(
            $"/api/sales?{filter}&_page=1&_size=4&_order=branchName%20desc%2CsaleNumber%20asc",
            token);
        Assert.Equal(
            new[] { "FUNC-PAGE-003", "FUNC-PAGE-004", "FUNC-PAGE-001", "FUNC-PAGE-002" },
            SaleNumbers(descendingBody.GetProperty("data")));
    }

    private async Task<string> SeedSalesAsync()
    {
        var (_, token) = await _api.CreateActiveUserAndAuthenticateAsync(
            "sales-query-filter@example.com",
            "Sales Query Filter");
        await _api.CreateSaleAsync(
            token,
            "FUNC-QUERY-ACTIVE",
            new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc),
            OtherCustomerId,
            "Other Customer",
            OtherBranchId,
            "Other Branch",
            Item(1));
        return token;
    }

    private async Task<string> SeedPagingSalesAsync()
    {
        var (_, token) = await _api.CreateActiveUserAndAuthenticateAsync(
            "sales-query-page@example.com",
            "Sales Query Page");
        var date = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        await _api.CreateSaleAsync(token, "FUNC-PAGE-001", date, PagingCustomerId, "Paging Customer", MatchingBranchId, "Alpha", Item(2));
        await _api.CreateSaleAsync(token, "FUNC-PAGE-002", date, PagingCustomerId, "Paging Customer", MatchingBranchId, "Alpha", Item(3));
        await _api.CreateSaleAsync(token, "FUNC-PAGE-003", date, PagingCustomerId, "Paging Customer", OtherBranchId, "Beta", Item(4));
        await _api.CreateSaleAsync(token, "FUNC-PAGE-004", date, PagingCustomerId, "Paging Customer", OtherBranchId, "Beta", Item(5));
        return token;
    }

    private static SaleItemInput Item(int suffix) => new(
        Guid.Parse($"32000000-0000-0000-0000-{suffix:000000000000}"),
        $"Query Product {suffix}",
        2,
        10m);

    private static string[] SaleNumbers(JsonElement data) => data.GetProperty("items")
        .EnumerateArray()
        .Select(item => item.GetProperty("saleNumber").GetString()!)
        .ToArray();
}
