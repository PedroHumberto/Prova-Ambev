using System.Reflection;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Ambev.DeveloperEvaluation.Domain.Sales.Enums;
using Ambev.DeveloperEvaluation.Domain.Sales.Events;
using Ambev.DeveloperEvaluation.Domain.Sales.Exceptions;
using Ambev.DeveloperEvaluation.Unit.Domain.Sales.TestData;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Sales;

public sealed class SaleTests
{
    [Fact]
    public void Create_ValidData_NormalizesSnapshotsAndRaisesSaleCreated()
    {
        // Arrange
        var clock = new TestTimeProvider(SaleTestData.CreatedAt);

        // Act
        var sale = Sale.Create(
            "  Sale-001  ",
            SaleTestData.SaleDate,
            SaleTestData.CustomerId,
            "  Customer One  ",
            SaleTestData.BranchId,
            "  Branch One  ",
            [SaleTestData.Item(productName: "  Product One  ")],
            clock);

        // Assert
        sale.Id.Should().NotBeEmpty();
        sale.SaleNumber.Should().Be("Sale-001");
        sale.CustomerName.Should().Be("Customer One");
        sale.BranchName.Should().Be("Branch One");
        sale.Status.Should().Be(SaleStatus.Active);
        sale.CreatedAt.Should().Be(SaleTestData.CreatedAt);
        sale.UpdatedAt.Should().Be(SaleTestData.CreatedAt);
        sale.Items.Should().ContainSingle().Which.ProductName.Should().Be("Product One");
        sale.DomainEvents.Should().ContainSingle()
            .Which.Should().Be(new SaleCreated(sale.Id, SaleTestData.CreatedAt));
    }

    [Fact]
    public void Create_TextAtNormalizedLimitsWithOuterSpaces_AcceptsAndStoresTrimmedValues()
    {
        var saleNumber = new string('S', 50);
        var customerName = new string('C', 200);
        var branchName = new string('B', 200);
        var productName = new string('P', 200);

        var sale = Sale.Create(
            $"  {saleNumber}  ",
            SaleTestData.SaleDate,
            SaleTestData.CustomerId,
            $"  {customerName}  ",
            SaleTestData.BranchId,
            $"  {branchName}  ",
            [SaleTestData.Item(productName: $"  {productName}  ")],
            new TestTimeProvider(SaleTestData.CreatedAt));

        sale.SaleNumber.Should().Be(saleNumber);
        sale.CustomerName.Should().Be(customerName);
        sale.BranchName.Should().Be(branchName);
        sale.Items.Single().ProductName.Should().Be(productName);
    }

    [Fact]
    public void Create_TextAboveNormalizedLimits_RejectsEverySnapshotField()
    {
        var validItem = SaleTestData.Item();

        var actions = new Action[]
        {
            () => Sale.Create(
                $" {new string('S', 51)} ",
                SaleTestData.SaleDate,
                SaleTestData.CustomerId,
                "Customer",
                SaleTestData.BranchId,
                "Branch",
                [validItem]),
            () => Sale.Create(
                "SALE-001",
                SaleTestData.SaleDate,
                SaleTestData.CustomerId,
                $" {new string('C', 201)} ",
                SaleTestData.BranchId,
                "Branch",
                [validItem]),
            () => Sale.Create(
                "SALE-001",
                SaleTestData.SaleDate,
                SaleTestData.CustomerId,
                "Customer",
                SaleTestData.BranchId,
                $" {new string('B', 201)} ",
                [validItem]),
            () => SaleTestData.CreateSale(
                items: [SaleTestData.Item(productName: $" {new string('P', 201)} ")])
        };

        actions.Should().AllSatisfy(action => action.Should().Throw<SalesDomainException>());
    }

    [Theory]
    [InlineData(1, 0, 10, 0, 10)]
    [InlineData(3, 0, 30, 0, 30)]
    [InlineData(4, 10, 40, 4, 36)]
    [InlineData(9, 10, 90, 9, 81)]
    [InlineData(10, 20, 100, 20, 80)]
    [InlineData(20, 20, 200, 40, 160)]
    public void Create_BoundaryQuantity_DerivesDiscountAndTotals(
        int quantity,
        int expectedDiscount,
        int expectedSubtotal,
        int expectedDiscountAmount,
        int expectedTotal)
    {
        // Arrange
        var input = SaleTestData.Item(quantity: quantity);

        // Act
        var sale = SaleTestData.CreateSale(items: [input]);

        // Assert
        var item = sale.Items.Single();
        item.DiscountPercentage.Should().Be(expectedDiscount);
        item.Subtotal.Should().Be(expectedSubtotal);
        item.DiscountAmount.Should().Be(expectedDiscountAmount);
        item.TotalAmount.Should().Be(expectedTotal);
        sale.Subtotal.Should().Be(item.Subtotal);
        sale.DiscountAmount.Should().Be(item.DiscountAmount);
        sale.TotalAmount.Should().Be(item.TotalAmount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(21)]
    public void Create_QuantityOutsideAllowedRange_ThrowsInvalidSaleItemException(int quantity)
    {
        // Arrange
        var input = SaleTestData.Item(quantity: quantity);

        // Act
        var act = () => SaleTestData.CreateSale(items: [input]);

        // Assert
        act.Should().Throw<InvalidSaleItemException>()
            .WithMessage("Quantity must be between 1 and 20.");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(10000000000000000)]
    public void Create_UnitPriceOutsideAllowedRange_ThrowsInvalidSaleItemException(decimal unitPrice)
    {
        // Arrange
        var input = SaleTestData.Item(unitPrice: unitPrice);

        // Act
        var act = () => SaleTestData.CreateSale(items: [input]);

        // Assert
        act.Should().Throw<InvalidSaleItemException>()
            .WithMessage($"Unit price must be between 0.01 and {9_999_999_999_999_999.99m}.");
    }

    [Fact]
    public void Create_DiscountMidpoint_RoundsAwayFromZero()
    {
        // Arrange
        var input = SaleTestData.Item(quantity: 5, unitPrice: 0.01m);

        // Act
        var item = SaleTestData.CreateSale(items: [input]).Items.Single();

        // Assert
        item.Subtotal.Should().Be(0.05m);
        item.DiscountAmount.Should().Be(0.01m);
        item.TotalAmount.Should().Be(0.04m);
    }

    [Fact]
    public void Create_DuplicateProduct_ThrowsDuplicateActiveProductException()
    {
        // Arrange
        var items = new[]
        {
            SaleTestData.Item(),
            SaleTestData.Item(productName: "Same product snapshot")
        };

        // Act
        var act = () => SaleTestData.CreateSale(items: items);

        // Assert
        act.Should().Throw<DuplicateActiveProductException>();
    }

    [Fact]
    public void Create_MultipleProducts_SumsRoundedLineTotals()
    {
        // Arrange
        var builder = new SaleBuilder().WithItems(
            SaleTestData.Item(quantity: 5, unitPrice: 0.01m),
            SaleTestData.Item(SaleTestData.ProductTwoId, "Product Two", 5, 0.01m));

        // Act
        var sale = builder.Build();

        // Assert
        sale.Items.Should().HaveCount(2);
        sale.Subtotal.Should().Be(0.10m);
        sale.DiscountAmount.Should().Be(0.02m);
        sale.TotalAmount.Should().Be(0.08m);
    }

    [Fact]
    public void AddItem_ValidItem_AddsItemRecalculatesTotalsAndRaisesSaleUpdated()
    {
        // Arrange
        var clock = new TestTimeProvider(SaleTestData.CreatedAt);
        var sale = SaleTestData.CreateSale(clock);
        sale.ClearDomainEvents();
        var updatedAt = SaleTestData.CreatedAt.AddMinutes(1);
        clock.SetUtcNow(updatedAt);

        // Act
        var added = sale.AddItem(SaleTestData.ProductTwoId, " Product Two ", 4, 5m);

        // Assert
        added.ProductName.Should().Be("Product Two");
        added.DiscountPercentage.Should().Be(10);
        sale.Items.Should().HaveCount(2);
        sale.Subtotal.Should().Be(30m);
        sale.DiscountAmount.Should().Be(2m);
        sale.TotalAmount.Should().Be(28m);
        sale.UpdatedAt.Should().Be(updatedAt);
        sale.DomainEvents.Should().ContainSingle().Which.Should().Be(new SaleUpdated(sale.Id, updatedAt));
    }

    [Fact]
    public void AddItem_DuplicateActiveProduct_ThrowsWithoutChangingAggregate()
    {
        // Arrange
        var sale = SaleTestData.CreateSale();
        var originalUpdatedAt = sale.UpdatedAt;
        sale.ClearDomainEvents();

        // Act
        var act = () => sale.AddItem(SaleTestData.ProductOneId, "Duplicate", 4, 5m);

        // Assert
        act.Should().Throw<DuplicateActiveProductException>();
        sale.Items.Should().ContainSingle();
        sale.Subtotal.Should().Be(10m);
        sale.DiscountAmount.Should().Be(0m);
        sale.TotalAmount.Should().Be(10m);
        sale.UpdatedAt.Should().Be(originalUpdatedAt);
        sale.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateHeader_ChangedSnapshots_NormalizesValuesAndRaisesSaleUpdated()
    {
        // Arrange
        var clock = new TestTimeProvider(SaleTestData.CreatedAt);
        var sale = SaleTestData.CreateSale(clock);
        sale.ClearDomainEvents();
        var updatedAt = SaleTestData.CreatedAt.AddMinutes(1);
        clock.SetUtcNow(updatedAt);

        // Act
        sale.UpdateHeader(
            "  SALE-UPDATED  ",
            SaleTestData.SaleDate.AddDays(1),
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            "  Updated Customer  ",
            Guid.Parse("77777777-7777-7777-7777-777777777777"),
            "  Updated Branch  ");

        // Assert
        sale.SaleNumber.Should().Be("SALE-UPDATED");
        sale.CustomerName.Should().Be("Updated Customer");
        sale.BranchName.Should().Be("Updated Branch");
        sale.UpdatedAt.Should().Be(updatedAt);
        sale.DomainEvents.Should().ContainSingle().Which.Should().Be(new SaleUpdated(sale.Id, updatedAt));
    }

    [Fact]
    public void UpdateHeader_EquivalentNormalizedSnapshots_IsNoOp()
    {
        // Arrange
        var clock = new TestTimeProvider(SaleTestData.CreatedAt);
        var sale = SaleTestData.CreateSale(clock);
        var originalUpdatedAt = sale.UpdatedAt;
        sale.ClearDomainEvents();
        clock.SetUtcNow(SaleTestData.CreatedAt.AddMinutes(1));

        // Act
        sale.UpdateHeader(
            $"  {sale.SaleNumber}  ",
            sale.SaleDate,
            sale.CustomerId,
            $"  {sale.CustomerName}  ",
            sale.BranchId,
            $"  {sale.BranchName}  ");

        // Assert
        sale.UpdatedAt.Should().Be(originalUpdatedAt);
        sale.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateItem_QuantityChangesDiscountBand_RecalculatesValuesAndRaisesSaleUpdated()
    {
        // Arrange
        var clock = new TestTimeProvider(SaleTestData.CreatedAt);
        var sale = SaleTestData.CreateSale(clock, SaleTestData.Item(quantity: 3));
        var item = sale.Items.Single();
        sale.ClearDomainEvents();
        var updatedAt = SaleTestData.CreatedAt.AddMinutes(1);
        clock.SetUtcNow(updatedAt);

        // Act
        sale.UpdateItem(item.Id, "Product One Updated", 10, 10m);

        // Assert
        item.ProductName.Should().Be("Product One Updated");
        item.DiscountPercentage.Should().Be(20);
        item.TotalAmount.Should().Be(80m);
        sale.TotalAmount.Should().Be(80m);
        item.UpdatedAt.Should().Be(updatedAt);
        sale.DomainEvents.Should().ContainSingle().Which.Should().Be(new SaleUpdated(sale.Id, updatedAt));
    }

    [Fact]
    public void UpdateItem_EquivalentNormalizedValues_IsNoOp()
    {
        // Arrange
        var clock = new TestTimeProvider(SaleTestData.CreatedAt);
        var sale = SaleTestData.CreateSale(clock);
        var item = sale.Items.Single();
        var originalUpdatedAt = sale.UpdatedAt;
        sale.ClearDomainEvents();
        clock.SetUtcNow(SaleTestData.CreatedAt.AddMinutes(1));

        // Act
        sale.UpdateItem(item.Id, $"  {item.ProductName}  ", item.Quantity, item.UnitPrice);

        // Assert
        item.UpdatedAt.Should().Be(originalUpdatedAt);
        sale.UpdatedAt.Should().Be(originalUpdatedAt);
        sale.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(3, 4, 10, 40, 4, 36)]
    [InlineData(4, 3, 0, 30, 0, 30)]
    [InlineData(9, 10, 20, 100, 20, 80)]
    [InlineData(10, 9, 10, 90, 9, 81)]
    public void UpdateItem_CrossesAdjacentDiscountBoundary_RecalculatesDiscount(
        int initialQuantity,
        int updatedQuantity,
        int expectedDiscount,
        int expectedSubtotal,
        int expectedDiscountAmount,
        int expectedTotal)
    {
        // Arrange
        var sale = SaleTestData.CreateSale(items: [SaleTestData.Item(quantity: initialQuantity)]);
        var item = sale.Items.Single();

        // Act
        sale.UpdateItem(item.Id, item.ProductName, updatedQuantity, item.UnitPrice);

        // Assert
        item.DiscountPercentage.Should().Be(expectedDiscount);
        item.Subtotal.Should().Be(expectedSubtotal);
        item.DiscountAmount.Should().Be(expectedDiscountAmount);
        item.TotalAmount.Should().Be(expectedTotal);
        sale.Subtotal.Should().Be(expectedSubtotal);
        sale.DiscountAmount.Should().Be(expectedDiscountAmount);
        sale.TotalAmount.Should().Be(expectedTotal);
    }

    [Fact]
    public void CancelItem_WithAnotherActiveItem_CancelsItemRecalculatesTotalsAndRaisesEvent()
    {
        // Arrange
        var clock = new TestTimeProvider(SaleTestData.CreatedAt);
        var sale = SaleTestData.CreateSale(
            clock,
            SaleTestData.Item(quantity: 10),
            SaleTestData.Item(SaleTestData.ProductTwoId, "Product Two", 1, 7m));
        var cancelledItem = sale.Items.First();
        sale.ClearDomainEvents();
        var cancelledAt = SaleTestData.CreatedAt.AddMinutes(1);
        clock.SetUtcNow(cancelledAt);

        // Act
        sale.CancelItem(cancelledItem.Id);

        // Assert
        cancelledItem.Status.Should().Be(SaleStatus.Cancelled);
        cancelledItem.CancelledAt.Should().Be(cancelledAt);
        cancelledItem.TotalAmount.Should().Be(80m);
        sale.Subtotal.Should().Be(7m);
        sale.DiscountAmount.Should().Be(0m);
        sale.TotalAmount.Should().Be(7m);
        sale.DomainEvents.Should().ContainSingle().Which
            .Should().Be(new SaleItemCancelled(sale.Id, cancelledItem.Id, cancelledAt));
    }

    [Fact]
    public void CancelItem_CalledTwice_IsIdempotent()
    {
        // Arrange
        var clock = new TestTimeProvider(SaleTestData.CreatedAt);
        var sale = SaleTestData.CreateSale(
            clock,
            SaleTestData.Item(),
            SaleTestData.Item(SaleTestData.ProductTwoId, "Product Two"));
        var item = sale.Items.First();
        sale.ClearDomainEvents();
        var cancelledAt = SaleTestData.CreatedAt.AddMinutes(1);
        clock.SetUtcNow(cancelledAt);

        // Act
        sale.CancelItem(item.Id);
        clock.SetUtcNow(cancelledAt.AddMinutes(1));
        sale.CancelItem(item.Id);

        // Assert
        item.CancelledAt.Should().Be(cancelledAt);
        item.UpdatedAt.Should().Be(cancelledAt);
        sale.UpdatedAt.Should().Be(cancelledAt);
        sale.TotalAmount.Should().Be(10m);
        sale.DomainEvents.Should().ContainSingle().Which
            .Should().Be(new SaleItemCancelled(sale.Id, item.Id, cancelledAt));
    }

    [Fact]
    public void CancelItem_LastActiveItem_ThrowsWithoutChangingAggregate()
    {
        // Arrange
        var sale = SaleTestData.CreateSale();
        var item = sale.Items.Single();
        sale.ClearDomainEvents();

        // Act
        var act = () => sale.CancelItem(item.Id);

        // Assert
        act.Should().Throw<LastActiveSaleItemException>();
        item.IsActive.Should().BeTrue();
        sale.TotalAmount.Should().Be(10m);
        sale.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Cancel_CalledTwice_IsIdempotentAndBlocksMutations()
    {
        // Arrange
        var clock = new TestTimeProvider(SaleTestData.CreatedAt);
        var sale = SaleTestData.CreateSale(clock);
        var item = sale.Items.Single();
        sale.ClearDomainEvents();
        var cancelledAt = SaleTestData.CreatedAt.AddMinutes(1);
        clock.SetUtcNow(cancelledAt);

        // Act
        sale.Cancel();
        clock.SetUtcNow(cancelledAt.AddMinutes(1));
        sale.Cancel();

        // Assert
        sale.Status.Should().Be(SaleStatus.Cancelled);
        sale.CancelledAt.Should().Be(cancelledAt);
        sale.UpdatedAt.Should().Be(cancelledAt);
        sale.DomainEvents.Should().ContainSingle().Which.Should().Be(new SaleCancelled(sale.Id, cancelledAt));
        new Action[]
        {
            () => sale.UpdateHeader(sale.SaleNumber, sale.SaleDate, sale.CustomerId, sale.CustomerName, sale.BranchId, sale.BranchName),
            () => sale.AddItem(SaleTestData.ProductTwoId, "Product Two", 1, 1m),
            () => sale.UpdateItem(item.Id, item.ProductName, item.Quantity, item.UnitPrice),
            () => sale.CancelItem(item.Id),
            () => sale.ReplaceEditableData(sale.SaleNumber, sale.SaleDate, sale.CustomerId, sale.CustomerName, sale.BranchId, sale.BranchName, [SaleTestData.Replacement(item)])
        }.Should().AllSatisfy(mutation => mutation.Should().Throw<CancelledSaleModificationException>());
    }

    [Fact]
    public void CancelledItem_ModificationIsRejectedButProductCanBeAddedAgain()
    {
        // Arrange
        var sale = SaleTestData.CreateSale(
            items:
            [
                SaleTestData.Item(),
                SaleTestData.Item(SaleTestData.ProductTwoId, "Product Two")
            ]);
        var cancelledItem = sale.Items.First();
        sale.CancelItem(cancelledItem.Id);

        // Act
        var update = () => sale.UpdateItem(cancelledItem.Id, "Changed", 2, 2m);
        var replacement = () => sale.ReplaceEditableData(
            sale.SaleNumber,
            sale.SaleDate,
            sale.CustomerId,
            sale.CustomerName,
            sale.BranchId,
            sale.BranchName,
            [SaleTestData.Replacement(cancelledItem)]);
        var added = sale.AddItem(cancelledItem.ProductId, "Product One Again", 2, 3m);

        // Assert
        update.Should().Throw<CancelledSaleItemModificationException>();
        replacement.Should().Throw<CancelledSaleItemModificationException>();
        added.Id.Should().NotBe(cancelledItem.Id);
        added.ProductId.Should().Be(cancelledItem.ProductId);
        added.IsActive.Should().BeTrue();
    }

    [Fact]
    public void ReplaceEditableData_LateValidationFailure_IsAtomic()
    {
        // Arrange
        var sale = SaleTestData.CreateSale(
            items:
            [
                SaleTestData.Item(),
                SaleTestData.Item(SaleTestData.ProductTwoId, "Product Two")
            ]);
        var first = sale.Items.First();
        var originalUpdatedAt = sale.UpdatedAt;
        sale.ClearDomainEvents();
        var replacements = new[]
        {
            new SaleItemReplacement(first.Id, first.ProductId, "Changed", 10, 99m),
            new SaleItemReplacement(Guid.NewGuid(), SaleTestData.ProductThreeId, "Unknown", 1, 1m)
        };

        // Act
        var act = () => sale.ReplaceEditableData(
            "CHANGED",
            sale.SaleDate,
            sale.CustomerId,
            "Changed Customer",
            sale.BranchId,
            sale.BranchName,
            replacements);

        // Assert
        act.Should().Throw<InvalidSaleItemException>();
        sale.SaleNumber.Should().Be("SALE-2026-0001");
        sale.CustomerName.Should().Be("Customer One");
        first.ProductName.Should().Be("Product One");
        first.Quantity.Should().Be(1);
        sale.Items.Should().OnlyContain(item => item.IsActive);
        sale.UpdatedAt.Should().Be(originalUpdatedAt);
        sale.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ReplaceEditableData_ActiveProductWithoutItsItemId_RejectsAmbiguousReplacementWithoutChanges()
    {
        // Arrange
        var sale = SaleTestData.CreateSale();
        var item = sale.Items.Single();
        var originalUpdatedAt = sale.UpdatedAt;
        sale.ClearDomainEvents();

        // Act
        var act = () => sale.ReplaceEditableData(
            sale.SaleNumber,
            sale.SaleDate,
            sale.CustomerId,
            sale.CustomerName,
            sale.BranchId,
            sale.BranchName,
            [new SaleItemReplacement(null, item.ProductId, item.ProductName, item.Quantity, item.UnitPrice)]);

        // Assert
        act.Should().Throw<InvalidSaleItemException>()
            .WithMessage($"The active item for product '{item.ProductId}' must retain its item ID.");
        sale.Items.Should().ContainSingle().Which.Should().BeSameAs(item);
        sale.UpdatedAt.Should().Be(originalUpdatedAt);
        sale.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CancelItem_InvalidItemId_ThrowsSpecificValidationException(bool emptyId)
    {
        // Arrange
        var sale = SaleTestData.CreateSale();
        var itemId = emptyId ? Guid.Empty : Guid.Parse("99999999-9999-9999-9999-999999999999");

        // Act
        var act = () => sale.CancelItem(itemId);

        // Assert
        if (emptyId)
        {
            act.Should().Throw<InvalidSaleItemException>()
                .WithMessage("Sale item ID cannot be empty.");
        }
        else
        {
            act.Should().Throw<SaleItemNotFoundException>()
                .WithMessage($"Sale item '{itemId}' does not belong to the sale.");
        }
    }

    [Fact]
    public void ReplaceEditableData_DuplicateProduct_ThrowsWithoutChangingAggregate()
    {
        // Arrange
        var sale = SaleTestData.CreateSale(
            items:
            [
                SaleTestData.Item(),
                SaleTestData.Item(SaleTestData.ProductTwoId, "Product Two")
            ]);
        var originalUpdatedAt = sale.UpdatedAt;
        sale.ClearDomainEvents();
        var replacements = sale.Items
            .Select(item => new SaleItemReplacement(
                item.Id,
                SaleTestData.ProductOneId,
                item.ProductName,
                item.Quantity,
                item.UnitPrice))
            .ToArray();

        // Act
        var act = () => sale.ReplaceEditableData(
            sale.SaleNumber,
            sale.SaleDate,
            sale.CustomerId,
            sale.CustomerName,
            sale.BranchId,
            sale.BranchName,
            replacements);

        // Assert
        act.Should().Throw<DuplicateActiveProductException>();
        sale.Items.Should().OnlyContain(item => item.IsActive);
        sale.Items.Select(item => item.ProductId).Should().BeEquivalentTo(
            [SaleTestData.ProductOneId, SaleTestData.ProductTwoId]);
        sale.TotalAmount.Should().Be(20m);
        sale.UpdatedAt.Should().Be(originalUpdatedAt);
        sale.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ReplaceEditableData_ChangedSet_UpdatesAddsCancelsAndRaisesExpectedEvents()
    {
        // Arrange
        var clock = new TestTimeProvider(SaleTestData.CreatedAt);
        var sale = SaleTestData.CreateSale(
            clock,
            SaleTestData.Item(),
            SaleTestData.Item(SaleTestData.ProductTwoId, "Product Two", 2, 5m));
        var retained = sale.Items.First();
        var omitted = sale.Items.Last();
        sale.ClearDomainEvents();
        var updatedAt = SaleTestData.CreatedAt.AddMinutes(1);
        clock.SetUtcNow(updatedAt);

        // Act
        sale.ReplaceEditableData(
            "SALE-UPDATED",
            sale.SaleDate,
            sale.CustomerId,
            sale.CustomerName,
            sale.BranchId,
            sale.BranchName,
            [
                new SaleItemReplacement(retained.Id, retained.ProductId, "Product One Updated", 4, 10m),
                new SaleItemReplacement(null, SaleTestData.ProductThreeId, "Product Three", 1, 2m)
            ]);

        // Assert
        sale.SaleNumber.Should().Be("SALE-UPDATED");
        retained.Quantity.Should().Be(4);
        omitted.Status.Should().Be(SaleStatus.Cancelled);
        sale.Items.Should().HaveCount(3).And.ContainSingle(item => item.ProductId == SaleTestData.ProductThreeId);
        sale.Subtotal.Should().Be(42m);
        sale.DiscountAmount.Should().Be(4m);
        sale.TotalAmount.Should().Be(38m);
        sale.DomainEvents.Should().Equal(
            new SaleItemCancelled(sale.Id, omitted.Id, updatedAt),
            new SaleUpdated(sale.Id, updatedAt));
    }

    [Fact]
    public void ReplaceEditableData_IdenticalData_IsNoOp()
    {
        // Arrange
        var clock = new TestTimeProvider(SaleTestData.CreatedAt);
        var sale = SaleTestData.CreateSale(clock);
        var originalUpdatedAt = sale.UpdatedAt;
        sale.ClearDomainEvents();
        clock.SetUtcNow(SaleTestData.CreatedAt.AddHours(1));

        // Act
        sale.ReplaceEditableData(
            sale.SaleNumber,
            sale.SaleDate,
            sale.CustomerId,
            sale.CustomerName,
            sale.BranchId,
            sale.BranchName,
            sale.Items.Select(SaleTestData.Replacement));

        // Assert
        sale.UpdatedAt.Should().Be(originalUpdatedAt);
        sale.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void PublicState_ExposesNoPublicMutationPath()
    {
        // Arrange
        var sale = SaleTestData.CreateSale();
        var saleProperties = typeof(Sale).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var itemProperties = typeof(SaleItem).GetProperties(BindingFlags.Instance | BindingFlags.Public);

        // Act
        var saleSetters = saleProperties.Where(property => property.SetMethod?.IsPublic == true);
        var itemSetters = itemProperties.Where(property => property.SetMethod?.IsPublic == true);
        var mutateItems = () => ((ICollection<SaleItem>)sale.Items).Clear();
        var mutateEvents = () => ((ICollection<IDomainEvent>)sale.DomainEvents).Clear();

        // Assert
        saleSetters.Should().BeEmpty();
        itemSetters.Should().BeEmpty();
        mutateItems.Should().Throw<NotSupportedException>();
        mutateEvents.Should().Throw<NotSupportedException>();
    }

    [Theory]
    [InlineData("saleNumber")]
    [InlineData("saleDate")]
    [InlineData("customerId")]
    [InlineData("customerName")]
    [InlineData("branchId")]
    [InlineData("branchName")]
    [InlineData("items")]
    [InlineData("productId")]
    [InlineData("productName")]
    [InlineData("unitPrice")]
    [InlineData("unitPriceScale")]
    public void Create_InvalidPrincipalInput_ThrowsDomainValidationException(string invalidField)
    {
        // Arrange
        var saleNumber = "SALE-001";
        var saleDate = SaleTestData.SaleDate;
        var customerId = SaleTestData.CustomerId;
        var customerName = "Customer";
        var branchId = SaleTestData.BranchId;
        var branchName = "Branch";
        IEnumerable<SaleItemInput> items = [SaleTestData.Item()];

        switch (invalidField)
        {
            case "saleNumber": saleNumber = " "; break;
            case "saleDate": saleDate = DateTime.SpecifyKind(saleDate, DateTimeKind.Local); break;
            case "customerId": customerId = Guid.Empty; break;
            case "customerName": customerName = " "; break;
            case "branchId": branchId = Guid.Empty; break;
            case "branchName": branchName = " "; break;
            case "items": items = []; break;
            case "productId": items = [SaleTestData.Item(Guid.Empty)]; break;
            case "productName": items = [SaleTestData.Item(productName: " ")]; break;
            case "unitPrice": items = [SaleTestData.Item(unitPrice: 0m)]; break;
            case "unitPriceScale": items = [SaleTestData.Item(unitPrice: 1.001m)]; break;
        }

        // Act
        var act = () => Sale.Create(
            saleNumber,
            saleDate,
            customerId,
            customerName,
            branchId,
            branchName,
            items,
            new TestTimeProvider(SaleTestData.CreatedAt));

        // Assert
        act.Should().Throw<SalesDomainException>();
    }

    [Fact]
    public void Create_ItemOrAggregateMonetaryValueExceedsStorageRange_ThrowsMonetaryValueOutOfRangeException()
    {
        // Arrange
        const decimal maximum = 9999999999999999.99m;
        var overflowingItem = () => SaleTestData.CreateSale(items: [SaleTestData.Item(quantity: 20, unitPrice: maximum)]);
        var overflowingAggregate = () => SaleTestData.CreateSale(
            items:
            [
                SaleTestData.Item(unitPrice: maximum),
                SaleTestData.Item(SaleTestData.ProductTwoId, "Product Two", unitPrice: maximum)
            ]);

        // Act, Assert
        overflowingItem.Should().Throw<MonetaryValueOutOfRangeException>();
        overflowingAggregate.Should().Throw<MonetaryValueOutOfRangeException>();
    }
}
