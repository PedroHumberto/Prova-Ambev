using System.Reflection;
using System.Text.Json;
using Ambev.DeveloperEvaluation.WebApi.Features.Sales;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.WebApi;

public sealed class SalesRequestContractTests
{
    [Fact]
    public void Validate_EmptyRouteIdentifiers_ReportsEachIdentifier()
    {
        var saleResult = new SaleIdRequestValidator().TestValidate(new SaleIdRequest());
        var itemResult = new CancelSaleItemRequestValidator().TestValidate(new CancelSaleItemRequest());

        saleResult.ShouldHaveValidationErrorFor(request => request.Id);
        itemResult.ShouldHaveValidationErrorFor(request => request.SaleId);
        itemResult.ShouldHaveValidationErrorFor(request => request.ItemId);
    }

    [Theory]
    [InlineData(0, 10, "PageNumber")]
    [InlineData(1, 0, "PageSize")]
    [InlineData(1, 101, "PageSize")]
    public void Validate_PaginationOutsideBounds_ReportsExpectedProperty(
        int pageNumber,
        int pageSize,
        string propertyName)
    {
        var request = new ListSalesRequest { PageNumber = pageNumber, PageSize = pageSize };

        var result = new ListSalesRequestValidator().TestValidate(request);

        result.Errors.Should().Contain(error => error.PropertyName == propertyName);
    }

    [Fact]
    public void Validate_PaginationOffsetAboveInt32_ReportsOffsetError()
    {
        var request = new ListSalesRequest
        {
            PageNumber = (int.MaxValue / 100) + 2,
            PageSize = 100
        };

        var result = new ListSalesRequestValidator().TestValidate(request);

        result.Errors.Should().ContainSingle(error =>
            error.PropertyName == string.Empty
            && error.ErrorMessage == "The requested page offset exceeds the supported range.");
    }

    [Fact]
    public void Validate_InvalidCreateBody_ReportsHeaderAndNestedItemProperties()
    {
        var request = new CreateSaleRequest
        {
            SaleDate = DateTime.SpecifyKind(new DateTime(2026, 7, 30), DateTimeKind.Local),
            Items = [new CreateSaleItemRequest { Quantity = 21, UnitPrice = 0m }]
        };

        var result = new CreateSaleRequestValidator().TestValidate(request);

        result.Errors.Select(error => error.PropertyName).Should().BeEquivalentTo(
            nameof(request.SaleNumber),
            nameof(request.SaleDate),
            nameof(request.CustomerId),
            nameof(request.CustomerName),
            nameof(request.BranchId),
            nameof(request.BranchName),
            "Items[0].ProductId",
            "Items[0].ProductName",
            "Items[0].Quantity",
            "Items[0].UnitPrice");
    }

    [Fact]
    public void Validate_UpdateBodyWithEmptyItemId_ReportsIndexedId()
    {
        var request = ValidUpdateRequest(Guid.Empty);

        var result = new UpdateSaleRequestValidator().TestValidate(request);

        result.Errors.Should().ContainSingle(error =>
            error.PropertyName == "Items[0].Id"
            && error.ErrorMessage == "Sale item ID cannot be empty when supplied.");
    }

    [Theory]
    [InlineData("{\"unexpected\":true}", typeof(CreateSaleRequest))]
    [InlineData("{\"unexpected\":true}", typeof(UpdateSaleRequest))]
    [InlineData("{\"unexpected\":true}", typeof(CreateSaleItemRequest))]
    [InlineData("{\"unexpected\":true}", typeof(UpdateSaleItemRequest))]
    public void Deserialize_UnknownBodyMember_ThrowsJsonException(string json, Type requestType)
    {
        var action = () => JsonSerializer.Deserialize(json, requestType);

        action.Should().Throw<JsonException>();
    }

    [Fact]
    public void SalesContract_ControllerRequiresAuthenticationAndUsesDocumentedQueryNames()
    {
        var controllerAttributes = typeof(SalesController).GetCustomAttributes(inherit: true);
        var pageAttribute = typeof(ListSalesRequest).GetProperty(nameof(ListSalesRequest.PageNumber))!
            .GetCustomAttribute<FromQueryAttribute>();
        var sizeAttribute = typeof(ListSalesRequest).GetProperty(nameof(ListSalesRequest.PageSize))!
            .GetCustomAttribute<FromQueryAttribute>();

        controllerAttributes.Should().Contain(attribute => attribute is AuthorizeAttribute);
        pageAttribute!.Name.Should().Be("_page");
        sizeAttribute!.Name.Should().Be("_size");
    }

    private static UpdateSaleRequest ValidUpdateRequest(Guid itemId) => new()
    {
        SaleNumber = "SALE-WEB-001",
        SaleDate = new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc),
        CustomerId = Guid.Parse("20000000-0000-0000-0000-000000000001"),
        CustomerName = "Customer",
        BranchId = Guid.Parse("30000000-0000-0000-0000-000000000001"),
        BranchName = "Branch",
        Items =
        [
            new UpdateSaleItemRequest
            {
                Id = itemId,
                ProductId = Guid.Parse("40000000-0000-0000-0000-000000000001"),
                ProductName = "Product",
                Quantity = 4,
                UnitPrice = 10m
            }
        ]
    };
}
