using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Support;

internal sealed class ApiTestClient(HttpClient client)
{
    public async Task<(HttpResponseMessage Response, JsonElement Body)> PostAsync(
        string path,
        object body,
        string? token = null)
    {
        using var request = CreateRequest(HttpMethod.Post, path, token);
        request.Content = JsonContent.Create(body);
        var response = await client.SendAsync(request);
        return (response, await ReadBodyAsync(response));
    }

    public async Task<(HttpResponseMessage Response, JsonElement Body)> PutAsync(
        string path,
        object body,
        string token)
    {
        using var request = CreateRequest(HttpMethod.Put, path, token);
        request.Content = JsonContent.Create(body);
        var response = await client.SendAsync(request);
        return (response, await ReadBodyAsync(response));
    }

    public async Task<(HttpResponseMessage Response, JsonElement Body)> GetAsync(
        string path,
        string? token = null)
    {
        using var request = CreateRequest(HttpMethod.Get, path, token);
        var response = await client.SendAsync(request);
        return (response, await ReadBodyAsync(response));
    }

    public async Task<(HttpResponseMessage Response, JsonElement Body)> DeleteAsync(
        string path,
        string? token = null)
    {
        using var request = CreateRequest(HttpMethod.Delete, path, token);
        var response = await client.SendAsync(request);
        return (response, await ReadBodyAsync(response));
    }

    public async Task<(Guid Id, string Token)> CreateActiveUserAndAuthenticateAsync(
        string email,
        string username)
    {
        const string password = "ValidPassword1!";
        var (createResponse, createBody) = await PostAsync("/api/Users", new
        {
            username,
            password,
            phone = "+5511999999999",
            email,
            status = 1,
            role = 3
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var (authResponse, authBody) = await PostAsync("/api/Auth", new { email, password });
        Assert.Equal(HttpStatusCode.OK, authResponse.StatusCode);
        return (
            createBody.GetProperty("data").GetProperty("id").GetGuid(),
            authBody.GetProperty("data").GetProperty("token").GetString()!);
    }

    public async Task<JsonElement> CreateSaleAsync(
        string token,
        string saleNumber,
        DateTime saleDate,
        Guid customerId,
        string customerName,
        Guid branchId,
        string branchName,
        params SaleItemInput[] items)
    {
        var (response, body) = await PostAsync("/api/sales", new
        {
            saleNumber,
            saleDate,
            customerId,
            customerName,
            branchId,
            branchName,
            items
        }, token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sale = body.GetProperty("data");
        var expectedLocation = $"/api/sales/{sale.GetProperty("id").GetGuid()}";
        Assert.NotNull(response.Headers.Location);
        Assert.Equal(
            expectedLocation,
            response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location.PathAndQuery
                : response.Headers.Location.OriginalString);
        return sale;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string? token)
    {
        var request = new HttpRequestMessage(method, path);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static async Task<JsonElement> ReadBodyAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(content))
            return default;
        using var document = JsonDocument.Parse(content);
        return document.RootElement.Clone();
    }
}

internal sealed record SaleItemInput(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);
