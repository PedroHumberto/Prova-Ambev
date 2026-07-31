using System.Reflection;
using System.Text.Json;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.WebApi.Common;
using Ambev.DeveloperEvaluation.WebApi.Features.Sales;
using FluentAssertions;
using FluentValidation;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using System.Globalization;
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
    public void Validate_AllWhitelistedOrderFieldsWithCaseInsensitiveDirections_HasNoErrors()
    {
        var request = new ListSalesRequest
        {
            Order = "ID,SALENUMBER DESC,saleDate aSc,customerName,branchName,status,subtotal,discountAmount,totalAmount,createdAt,updatedAt"
        };

        var result = new ListSalesRequestValidator().TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("unknown")]
    [InlineData("id sideways")]
    [InlineData("id asc extra")]
    [InlineData("id,,saleDate")]
    [InlineData("id,id desc")]
    public void Validate_InvalidOrder_ReportsOrder(string order)
    {
        var request = new ListSalesRequest { Order = order };

        var result = new ListSalesRequestValidator().TestValidate(request);

        result.ShouldHaveValidationErrorFor(candidate => candidate.Order);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("cancelled")]
    [InlineData("Unknown")]
    [InlineData("")]
    public void Validate_InvalidCaseSensitiveStatus_ReportsStatus(string status)
    {
        var request = new ListSalesRequest { Status = status };

        var result = new ListSalesRequestValidator().TestValidate(request);

        result.ShouldHaveValidationErrorFor(candidate => candidate.Status)
            .WithErrorMessage("Status must be Active or Cancelled.");
    }

    [Fact]
    public void Validate_ReversedDateRangeAndBlankFilters_ReportsExactProperties()
    {
        var request = new ListSalesRequest
        {
            SaleDateFrom = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            SaleDateTo = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            SaleNumber = " ",
            CustomerName = " ",
            BranchName = " "
        };

        var result = new ListSalesRequestValidator().TestValidate(request);

        result.Errors.Select(error => error.PropertyName).Should().BeEquivalentTo(
            string.Empty,
            nameof(request.SaleNumber),
            nameof(request.CustomerName),
            nameof(request.BranchName));
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

    [Theory]
    [InlineData("2026-07-30T14:30:00Z", "2026-07-30T14:30:00.0000000+00:00")]
    [InlineData("2026-07-30T14:30:00.1Z", "2026-07-30T14:30:00.1000000+00:00")]
    [InlineData("2026-07-30T14:30:00.1234567Z", "2026-07-30T14:30:00.1234567+00:00")]
    public async Task BindModelAsync_StrictUtcInstant_SetsSuccessfulResult(string value, string expected)
    {
        var bindingContext = CreateBindingContext("saleDateFrom", value);

        await new UtcQueryInstantModelBinder().BindModelAsync(bindingContext);

        bindingContext.ModelState.IsValid.Should().BeTrue();
        bindingContext.Result.IsModelSet.Should().BeTrue();
        bindingContext.Result.Model.Should().Be(DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task BindModelAsync_ValidInstantWithPreexistingError_PreservesInvalidModelState()
    {
        var bindingContext = CreateBindingContext("saleDateFrom", "2026-07-30T14:30:00Z");
        bindingContext.ModelState.TryAddModelError("saleDateFrom", "Preexisting model state error.");

        await new UtcQueryInstantModelBinder().BindModelAsync(bindingContext);

        bindingContext.Result.IsModelSet.Should().BeTrue();
        bindingContext.Result.Model.Should().Be(
            new DateTimeOffset(2026, 7, 30, 14, 30, 0, TimeSpan.Zero));
        bindingContext.ModelState.IsValid.Should().BeFalse();
        bindingContext.ModelState["saleDateFrom"]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Preexisting model state error.");
    }

    [Fact]
    public async Task BindModelAsync_ValidInstantWithPreexistingInvalidState_PreservesInvalidState()
    {
        var bindingContext = CreateBindingContext("saleDateFrom", "2026-07-30T14:30:00Z");
        bindingContext.ModelState.SetModelValue("saleDateFrom", "existing", "existing");
        bindingContext.ModelState["saleDateFrom"]!.ValidationState = ModelValidationState.Invalid;

        await new UtcQueryInstantModelBinder().BindModelAsync(bindingContext);

        bindingContext.Result.IsModelSet.Should().BeTrue();
        bindingContext.Result.Model.Should().Be(
            new DateTimeOffset(2026, 7, 30, 14, 30, 0, TimeSpan.Zero));
        bindingContext.ModelState.IsValid.Should().BeFalse();
        bindingContext.ModelState["saleDateFrom"]!.ValidationState.Should().Be(ModelValidationState.Invalid);
        bindingContext.ModelState["saleDateFrom"]!.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("2026-07-30")]
    [InlineData("2026-07-30t14:30:00Z")]
    [InlineData("2026-07-30T14:30:00z")]
    [InlineData("2026-07-30T14:30:00+00:00")]
    [InlineData("2026-07-30T14:30:00.12345678Z")]
    [InlineData("2026-02-30T14:30:00Z")]
    public async Task BindModelAsync_MalformedUtcInstant_AddsFormatModelStateError(string value)
    {
        var bindingContext = CreateBindingContext("saleDateFrom", value);

        await new UtcQueryInstantModelBinder().BindModelAsync(bindingContext);

        bindingContext.Result.IsModelSet.Should().BeFalse();
        bindingContext.ModelState["saleDateFrom"]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(
                "Value must be a UTC instant in the format yyyy-MM-ddTHH:mm:ss[.fffffff]Z.");
    }

    [Fact]
    public async Task BindModelAsync_RepeatedUtcInstant_AddsSingleValueModelStateError()
    {
        var bindingContext = CreateBindingContext(
            "saleDateFrom",
            "2026-07-30T14:30:00Z",
            "2026-07-31T14:30:00Z");

        await new UtcQueryInstantModelBinder().BindModelAsync(bindingContext);

        bindingContext.Result.IsModelSet.Should().BeFalse();
        bindingContext.ModelState["saleDateFrom"]!.Errors.Should().ContainSingle()
             .Which.ErrorMessage.Should().Be("Query parameter must be supplied only once.");
    }

    [Theory]
    [InlineData("customerId")]
    [InlineData("branchId")]
    public async Task BindModelAsync_AbsentUuidParameter_LeavesOptionalModelUnset(string modelName)
    {
        var bindingContext = CreateUuidBindingContext(modelName);

        await new CanonicalUuidQueryModelBinder().BindModelAsync(bindingContext);

        bindingContext.Result.IsModelSet.Should().BeFalse();
        bindingContext.ModelState.Should().BeEmpty();
    }

    [Theory]
    [InlineData("customerId")]
    [InlineData("branchId")]
    public async Task BindModelAsync_CanonicalUuid_SetsSuccessfulResult(string modelName)
    {
        const string value = "abcdef01-2345-6789-abcd-ef0123456789";
        var bindingContext = CreateUuidBindingContext(modelName, value);

        await new CanonicalUuidQueryModelBinder().BindModelAsync(bindingContext);

        bindingContext.Result.IsModelSet.Should().BeTrue();
        bindingContext.Result.Model.Should().Be(Guid.Parse(value));
        bindingContext.ModelState.IsValid.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(InvalidCanonicalUuids))]
    public async Task BindModelAsync_NonCanonicalUuid_AddsFormatModelStateError(
        string modelName,
        string value)
    {
        var bindingContext = CreateUuidBindingContext(modelName, value);

        await new CanonicalUuidQueryModelBinder().BindModelAsync(bindingContext);

        bindingContext.Result.IsModelSet.Should().BeFalse();
        bindingContext.ModelState[modelName]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(
                "Value must be a non-empty lowercase UUID in the format xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx.");
    }

    [Theory]
    [InlineData("customerId")]
    [InlineData("branchId")]
    public async Task BindModelAsync_RepeatedUuid_AddsSingleValueModelStateError(string modelName)
    {
        var bindingContext = CreateUuidBindingContext(
            modelName,
            "abcdef01-2345-6789-abcd-ef0123456789",
            "abcdef01-2345-6789-abcd-ef0123456789");

        await new CanonicalUuidQueryModelBinder().BindModelAsync(bindingContext);

        bindingContext.Result.IsModelSet.Should().BeFalse();
        bindingContext.ModelState[modelName]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Query parameter must be supplied only once.");
    }

    [Theory]
    [InlineData("customerId")]
    [InlineData("branchId")]
    public async Task BindModelAsync_UuidWithPreexistingError_PreservesOriginalError(string modelName)
    {
        const string value = "abcdef01-2345-6789-abcd-ef0123456789";
        var bindingContext = CreateUuidBindingContext(modelName, value);
        bindingContext.ModelState.TryAddModelError(modelName, "Preexisting model state error.");

        await new CanonicalUuidQueryModelBinder().BindModelAsync(bindingContext);

        bindingContext.Result.IsModelSet.Should().BeTrue();
        bindingContext.Result.Model.Should().Be(Guid.Parse(value));
        bindingContext.ModelState.IsValid.Should().BeFalse();
        bindingContext.ModelState[modelName]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Preexisting model state error.");
    }

    [Theory]
    [InlineData("customerId")]
    [InlineData("branchId")]
    public async Task BindModelAsync_ValidUuidWithPreexistingInvalidState_PreservesInvalidState(string modelName)
    {
        const string value = "abcdef01-2345-6789-abcd-ef0123456789";
        var bindingContext = CreateUuidBindingContext(modelName, value);
        bindingContext.ModelState.SetModelValue(modelName, "existing", "existing");
        bindingContext.ModelState[modelName]!.ValidationState = ModelValidationState.Invalid;

        await new CanonicalUuidQueryModelBinder().BindModelAsync(bindingContext);

        bindingContext.Result.IsModelSet.Should().BeTrue();
        bindingContext.Result.Model.Should().Be(Guid.Parse(value));
        bindingContext.ModelState.IsValid.Should().BeFalse();
        bindingContext.ModelState[modelName]!.ValidationState.Should().Be(ModelValidationState.Invalid);
    }

    [Theory]
    [InlineData(nameof(ListSalesRequest.CustomerId), "customerId")]
    [InlineData(nameof(ListSalesRequest.BranchId), "branchId")]
    public void CanonicalUuidQueryAttribute_FilterProperty_PreservesQueryBindingMetadata(
        string propertyName,
        string expectedModelName)
    {
        var property = typeof(ListSalesRequest).GetProperty(propertyName)!;
        var attribute = property.GetCustomAttribute<CanonicalUuidQueryAttribute>()!;

        property.PropertyType.Should().Be(typeof(Guid?));
        attribute.Name.Should().Be(expectedModelName);
        attribute.BindingSource.Should().Be(BindingSource.Query);
        attribute.BinderType.Should().Be<CanonicalUuidQueryModelBinder>();
    }

    [Fact]
    public void OnActionExecuting_AllDocumentedParametersWithSingleValues_DoesNotThrow()
    {
        var context = CreateActionExecutingContext(
            ("_page", "1"),
            ("_size", "10"),
            ("_order", "saleDate desc"),
            ("saleNumber", "SALE-1"),
            ("saleDateFrom", "2026-07-01T00:00:00Z"),
            ("saleDateTo", "2026-07-31T23:59:59Z"),
            ("customerId", Guid.NewGuid().ToString()),
            ("customerName", "Customer"),
            ("branchId", Guid.NewGuid().ToString()),
            ("branchName", "Branch"),
            ("status", "Active"));

        var action = () => GetStrictQueryAttribute().OnActionExecuting(context);

        action.Should().NotThrow();
    }

    [Theory]
    [MemberData(nameof(InvalidStrictQueries))]
    public void OnActionExecuting_UnsupportedRepeatedOrEmptyParameter_ThrowsValidationException(
        (string Key, string Value)[] query,
        string expectedProperty,
        string expectedMessage)
    {
        var context = CreateActionExecutingContext(query);

        var action = () => GetStrictQueryAttribute().OnActionExecuting(context);

        var exception = action.Should().Throw<ValidationException>().Which;
        exception.Errors.Should().ContainSingle(error =>
            error.PropertyName == expectedProperty && error.ErrorMessage == expectedMessage);
    }

    public static TheoryData<(string Key, string Value)[], string, string> InvalidStrictQueries => new()
    {
        {
            [("unknown", "value")],
            "unknown",
            "Query parameter is not supported."
        },
        {
            [("status", "Active"), ("status", "Cancelled")],
            "status",
            "Query parameter must be supplied only once."
        },
        {
            [("_order", string.Empty)],
            "_order",
            "Query parameter must not be empty."
        }
    };

    public static TheoryData<string, string> InvalidCanonicalUuids
    {
        get
        {
            var invalidValues = new[]
            {
                "00000000-0000-0000-0000-000000000000",
                "ABCDEF01-2345-6789-ABCD-EF0123456789",
                "abcdef0123456789abcdef0123456789",
                "{abcdef01-2345-6789-abcd-ef0123456789}",
                "(abcdef01-2345-6789-abcd-ef0123456789)",
                " abcdef01-2345-6789-abcd-ef0123456789 ",
                "not-a-uuid"
            };
            var data = new TheoryData<string, string>();
            foreach (var modelName in new[] { "customerId", "branchId" })
            {
                foreach (var value in invalidValues)
                    data.Add(modelName, value);
            }
            return data;
        }
    }

    private static DefaultModelBindingContext CreateBindingContext(string modelName, params string[] values)
    {
        var metadataProvider = new EmptyModelMetadataProvider();
        return new DefaultModelBindingContext
        {
            ActionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            ModelMetadata = metadataProvider.GetMetadataForType(typeof(DateTimeOffset?)),
            ModelName = modelName,
            ModelState = new ModelStateDictionary(),
            ValueProvider = new QueryStringValueProvider(
                BindingSource.Query,
                new QueryCollection(new Dictionary<string, StringValues>
                {
                    [modelName] = new StringValues(values)
                }),
                CultureInfo.InvariantCulture)
        };
    }

    private static DefaultModelBindingContext CreateUuidBindingContext(
        string modelName,
        params string[] values)
    {
        var metadataProvider = new EmptyModelMetadataProvider();
        var query = values.Length == 0
            ? new QueryCollection()
            : new QueryCollection(new Dictionary<string, StringValues>
            {
                [modelName] = new StringValues(values)
            });
        return new DefaultModelBindingContext
        {
            ActionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            ModelMetadata = metadataProvider.GetMetadataForType(typeof(Guid?)),
            ModelName = modelName,
            ModelState = new ModelStateDictionary(),
            ValueProvider = new QueryStringValueProvider(BindingSource.Query, query, CultureInfo.InvariantCulture)
        };
    }

    private static ActionExecutingContext CreateActionExecutingContext(
        params (string Key, string Value)[] query)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = QueryString.Create(query.Select(value =>
            new KeyValuePair<string, string?>(value.Key, value.Value)));
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?>(),
            new object());
    }

    private static StrictQueryParametersAttribute GetStrictQueryAttribute() =>
        typeof(SalesController).GetMethod(nameof(SalesController.List))!
            .GetCustomAttribute<StrictQueryParametersAttribute>()!;

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
