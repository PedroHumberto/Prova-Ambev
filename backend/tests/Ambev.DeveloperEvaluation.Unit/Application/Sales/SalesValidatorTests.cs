using Ambev.DeveloperEvaluation.Application.Sales.CancelSale;
using Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;
using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Application.Sales.GetSaleById;
using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Sales.Enums;
using Ambev.DeveloperEvaluation.Unit.Application.Sales.TestData;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public sealed class CreateSaleCommandValidatorTests
{
    private readonly CreateSaleCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_HasNoErrors()
    {
        var result = _validator.TestValidate(ApplicationSaleTestData.CreateCommand());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_TextAtNormalizedLimitsWithOuterSpaces_HasNoErrors()
    {
        var command = ApplicationSaleTestData.CreateCommand() with
        {
            SaleNumber = $"  {new string('S', 50)}  ",
            CustomerName = $"  {new string('C', 200)}  ",
            BranchName = $"  {new string('B', 200)}  ",
            Items =
            [
                ApplicationSaleTestData.CreateCommand().Items.First() with
                {
                    ProductName = $"  {new string('P', 200)}  "
                }
            ]
        };

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_TextAboveNormalizedLimits_ReportsEverySnapshotProperty()
    {
        var command = ApplicationSaleTestData.CreateCommand() with
        {
            SaleNumber = $" {new string('S', 51)} ",
            CustomerName = $" {new string('C', 201)} ",
            BranchName = $" {new string('B', 201)} ",
            Items =
            [
                ApplicationSaleTestData.CreateCommand().Items.First() with
                {
                    ProductName = $" {new string('P', 201)} "
                }
            ]
        };

        var result = _validator.TestValidate(command);

        result.Errors.Select(error => error.PropertyName).Should().BeEquivalentTo(
            nameof(command.SaleNumber),
            nameof(command.CustomerName),
            nameof(command.BranchName),
            "Items[0].ProductName");
    }

    [Fact]
    public void Validate_WhitespaceOnlyText_ReportsEverySnapshotProperty()
    {
        var command = ApplicationSaleTestData.CreateCommand() with
        {
            SaleNumber = "   ",
            CustomerName = "   ",
            BranchName = "   ",
            Items =
            [
                ApplicationSaleTestData.CreateCommand().Items.First() with
                {
                    ProductName = "   "
                }
            ]
        };

        var result = _validator.TestValidate(command);

        result.Errors.Select(error => error.PropertyName).Should().BeEquivalentTo(
            nameof(command.SaleNumber),
            nameof(command.CustomerName),
            nameof(command.BranchName),
            "Items[0].ProductName");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_NullOrEmptyItems_HasRequiredCollectionError(bool useNull)
    {
        var command = ApplicationSaleTestData.CreateCommand() with
        {
            Items = useNull ? null! : []
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(candidate => candidate.Items)
            .WithErrorCode("NotEmptyValidator");
    }

    [Fact]
    public void Validate_NullItem_HasIndexedNotNullError()
    {
        var command = ApplicationSaleTestData.CreateCommand() with
        {
            Items = [null!]
        };

        var result = _validator.TestValidate(command);

        result.Errors.Should().ContainSingle(error =>
            error.PropertyName == "Items[0]" && error.ErrorCode == "NotNullValidator");
    }

    [Fact]
    public void Validate_InvalidChildFields_ReportsEachIndexedProperty()
    {
        var command = ApplicationSaleTestData.CreateCommand() with
        {
            Items =
            [
                new CreateSaleItem
                {
                    ProductId = Guid.Empty,
                    ProductName = string.Empty,
                    Quantity = 21,
                    UnitPrice = 1.001m
                }
            ]
        };

        var result = _validator.TestValidate(command);

        result.Errors.Select(error => error.PropertyName).Should().BeEquivalentTo(
            "Items[0].ProductId",
            "Items[0].ProductName",
            "Items[0].Quantity",
            "Items[0].UnitPrice");
    }

    [Fact]
    public void Validate_InvalidHeaderFields_ReportsAllHeaderProperties()
    {
        var command = ApplicationSaleTestData.CreateCommand() with
        {
            SaleNumber = string.Empty,
            SaleDate = DateTime.SpecifyKind(ApplicationSaleTestData.SaleDate, DateTimeKind.Local),
            CustomerId = Guid.Empty,
            CustomerName = string.Empty,
            BranchId = Guid.Empty,
            BranchName = string.Empty
        };

        var result = _validator.TestValidate(command);

        result.Errors.Select(error => error.PropertyName).Should().BeEquivalentTo(
            nameof(command.SaleNumber),
            nameof(command.SaleDate),
            nameof(command.CustomerId),
            nameof(command.CustomerName),
            nameof(command.BranchId),
            nameof(command.BranchName));
    }
}

public sealed class UpdateSaleCommandValidatorTests
{
    private readonly UpdateSaleCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_HasNoErrors()
    {
        var sale = ApplicationSaleTestData.CreateSale();

        var result = _validator.TestValidate(ApplicationSaleTestData.CreateUpdateCommand(sale));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_TextAtNormalizedLimitsWithOuterSpaces_HasNoErrors()
    {
        var sale = ApplicationSaleTestData.CreateSale();
        var command = ApplicationSaleTestData.CreateUpdateCommand(sale);
        command = command with
        {
            SaleNumber = $"  {new string('S', 50)}  ",
            CustomerName = $"  {new string('C', 200)}  ",
            BranchName = $"  {new string('B', 200)}  ",
            Items = command.Items.Select((item, index) => index == 0
                ? item with { ProductName = $"  {new string('P', 200)}  " }
                : item).ToArray()
        };

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptySaleAndSuppliedItemIds_ReportsBothProperties()
    {
        var sale = ApplicationSaleTestData.CreateSale();
        var command = ApplicationSaleTestData.CreateUpdateCommand(sale) with
        {
            Id = Guid.Empty,
            Items =
            [
                ApplicationSaleTestData.CreateUpdateCommand(sale).Items.First() with
                {
                    Id = Guid.Empty
                }
            ]
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(candidate => candidate.Id);
        result.Errors.Should().ContainSingle(error =>
            error.PropertyName == "Items[0].Id"
            && error.ErrorMessage == "Sale item ID cannot be empty when supplied.");
    }

    [Fact]
    public void Validate_NullItem_HasIndexedNotNullError()
    {
        var sale = ApplicationSaleTestData.CreateSale();
        var command = ApplicationSaleTestData.CreateUpdateCommand(sale) with
        {
            Items = [null!]
        };

        var result = _validator.TestValidate(command);

        result.Errors.Should().ContainSingle(error =>
            error.PropertyName == "Items[0]" && error.ErrorCode == "NotNullValidator");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_NullOrEmptyItems_HasRequiredCollectionError(bool useNull)
    {
        var sale = ApplicationSaleTestData.CreateSale();
        var command = ApplicationSaleTestData.CreateUpdateCommand(sale) with
        {
            Items = useNull ? null! : []
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(candidate => candidate.Items)
            .WithErrorCode("NotEmptyValidator");
    }
}

public sealed class ListSalesQueryValidatorTests
{
    private readonly ListSalesQueryValidator _validator = new();

    [Fact]
    public void Defaults_NewQuery_AreValidAndDocumentedValues()
    {
        var query = new ListSalesQuery();

        var result = _validator.TestValidate(query);

        query.PageNumber.Should().Be(1);
        query.PageSize.Should().Be(10);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("CustomerId")]
    [InlineData("BranchId")]
    public void Validate_NullOptionalId_HasNoErrors(string propertyName)
    {
        var query = propertyName == nameof(ListSalesQuery.CustomerId)
            ? new ListSalesQuery { CustomerId = null }
            : new ListSalesQuery { BranchId = null };

        var result = _validator.TestValidate(query);

        result.ShouldNotHaveValidationErrorFor(propertyName);
    }

    [Theory]
    [InlineData("CustomerId", "Customer ID cannot be empty when specified.")]
    [InlineData("BranchId", "Branch ID cannot be empty when specified.")]
    public void Validate_EmptyOptionalId_ReportsExactPropertyAndMessage(
        string propertyName,
        string expectedMessage)
    {
        var query = propertyName == nameof(ListSalesQuery.CustomerId)
            ? new ListSalesQuery { CustomerId = Guid.Empty }
            : new ListSalesQuery { BranchId = Guid.Empty };

        var result = _validator.TestValidate(query);

        result.Errors.Should().ContainSingle(error =>
            error.PropertyName == propertyName && error.ErrorMessage == expectedMessage);
    }

    [Theory]
    [InlineData("CustomerId")]
    [InlineData("BranchId")]
    public void Validate_NonEmptyOptionalId_HasNoErrors(string propertyName)
    {
        var id = Guid.Parse("abcdef01-2345-6789-abcd-ef0123456789");
        var query = propertyName == nameof(ListSalesQuery.CustomerId)
            ? new ListSalesQuery { CustomerId = id }
            : new ListSalesQuery { BranchId = id };

        var result = _validator.TestValidate(query);

        result.ShouldNotHaveValidationErrorFor(propertyName);
    }

    [Theory]
    [InlineData(0, 10, "PageNumber")]
    [InlineData(1, 0, "PageSize")]
    [InlineData(1, 101, "PageSize")]
    public void Validate_PageOutsideSimpleBounds_ReportsExpectedProperty(
        int pageNumber,
        int pageSize,
        string propertyName)
    {
        var query = new ListSalesQuery { PageNumber = pageNumber, PageSize = pageSize };

        var result = _validator.TestValidate(query);

        result.Errors.Should().Contain(error => error.PropertyName == propertyName);
    }

    [Fact]
    public void Validate_LargestSupportedOffset_HasNoErrors()
    {
        var query = new ListSalesQuery
        {
            PageNumber = (int.MaxValue / ListSalesQuery.MaximumPageSize) + 1,
            PageSize = ListSalesQuery.MaximumPageSize
        };

        var result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_OffsetAboveInt32_ReportsCombinedRule()
    {
        var query = new ListSalesQuery
        {
            PageNumber = (int.MaxValue / ListSalesQuery.MaximumPageSize) + 2,
            PageSize = ListSalesQuery.MaximumPageSize
        };

        var result = _validator.TestValidate(query);

        result.Errors.Should().ContainSingle(error =>
            error.PropertyName == string.Empty
            && error.ErrorMessage == "The requested page offset exceeds the supported range.");
    }

    [Theory]
    [InlineData("SaleNumber")]
    [InlineData("CustomerName")]
    [InlineData("BranchName")]
    public void Validate_SpecifiedTextIsBlank_ReportsExactProperty(string propertyName)
    {
        var query = propertyName switch
        {
            nameof(ListSalesQuery.SaleNumber) => new ListSalesQuery { SaleNumber = "  " },
            nameof(ListSalesQuery.CustomerName) => new ListSalesQuery { CustomerName = "  " },
            _ => new ListSalesQuery { BranchName = "  " }
        };

        var result = _validator.TestValidate(query);

        result.Errors.Should().ContainSingle(error => error.PropertyName == propertyName);
    }

    [Fact]
    public void Validate_NonUtcAndReversedDateRange_ReportsBothDatesAndRange()
    {
        var query = new ListSalesQuery
        {
            SaleDateFrom = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Local),
            SaleDateTo = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Unspecified)
        };

        var result = _validator.TestValidate(query);

        result.Errors.Select(error => error.PropertyName).Should().BeEquivalentTo(
            nameof(query.SaleDateFrom),
            nameof(query.SaleDateTo),
            string.Empty);
    }

    [Fact]
    public void Validate_UndefinedStatus_ReportsStatus()
    {
        var query = new ListSalesQuery { Status = (SaleStatus)999 };

        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(candidate => candidate.Status);
    }

    [Fact]
    public void Validate_NullOrder_ReportsOrderWithoutThrowing()
    {
        var query = new ListSalesQuery { Order = null! };

        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(candidate => candidate.Order)
            .WithErrorMessage("Order must not be null.");
    }

    [Fact]
    public void Validate_NullOrderElement_ReportsIndexedElementWithoutThrowing()
    {
        var query = new ListSalesQuery { Order = [null!] };

        var result = _validator.TestValidate(query);

        result.Errors.Should().ContainSingle(error =>
            error.PropertyName == "Order[0]"
            && error.ErrorMessage == "Order clause must not be null.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_UndefinedOrderEnum_ReportsIndexedProperty(bool invalidField)
    {
        var clause = invalidField
            ? new SaleSortClause((SaleSortField)999, SortDirection.Ascending)
            : new SaleSortClause(SaleSortField.Id, (SortDirection)999);
        var query = new ListSalesQuery { Order = [clause] };

        var result = _validator.TestValidate(query);

        result.Errors.Should().ContainSingle(error =>
            error.PropertyName == (invalidField ? "Order[0].Field" : "Order[0].Direction"));
    }

    [Fact]
    public void Validate_DuplicateOrderField_ReportsCollectionRule()
    {
        var query = new ListSalesQuery
        {
            Order =
            [
                new SaleSortClause(SaleSortField.TotalAmount, SortDirection.Ascending),
                new SaleSortClause(SaleSortField.TotalAmount, SortDirection.Descending)
            ]
        };

        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(candidate => candidate.Order)
            .WithErrorMessage("Order fields must not be repeated.");
    }
}

public sealed class SaleIdentifierValidatorTests
{
    [Fact]
    public void Validate_EmptyGetSaleId_HasIdError()
    {
        var result = new GetSaleByIdQueryValidator().TestValidate(new GetSaleByIdQuery(Guid.Empty));

        result.ShouldHaveValidationErrorFor(query => query.Id);
    }

    [Fact]
    public void Validate_EmptyCancelSaleId_HasIdError()
    {
        var result = new CancelSaleCommandValidator().TestValidate(new CancelSaleCommand(Guid.Empty));

        result.ShouldHaveValidationErrorFor(command => command.Id);
    }

    [Fact]
    public void Validate_EmptyCancelItemIds_HasBothErrors()
    {
        var result = new CancelSaleItemCommandValidator().TestValidate(
            new CancelSaleItemCommand(Guid.Empty, Guid.Empty));

        result.ShouldHaveValidationErrorFor(command => command.SaleId);
        result.ShouldHaveValidationErrorFor(command => command.SaleItemId);
    }
}
