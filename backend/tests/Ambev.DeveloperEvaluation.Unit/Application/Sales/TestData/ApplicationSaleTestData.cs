using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using NSubstitute;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales.TestData;

internal static class ApplicationSaleTestData
{
    internal static readonly Guid SaleId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    internal static readonly Guid CustomerId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    internal static readonly Guid BranchId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    internal static readonly Guid ProductOneId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    internal static readonly Guid ProductTwoId = Guid.Parse("40000000-0000-0000-0000-000000000002");
    internal static readonly Guid ProductThreeId = Guid.Parse("40000000-0000-0000-0000-000000000003");
    internal static readonly DateTime SaleDate = new(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);

    internal static CreateSaleCommand CreateCommand() => new()
    {
        SaleNumber = "SALE-APP-001",
        SaleDate = SaleDate,
        CustomerId = CustomerId,
        CustomerName = "Customer One",
        BranchId = BranchId,
        BranchName = "Branch One",
        Items =
        [
            new CreateSaleItem
            {
                ProductId = ProductOneId,
                ProductName = "Product One",
                Quantity = 4,
                UnitPrice = 10m
            },
            new CreateSaleItem
            {
                ProductId = ProductTwoId,
                ProductName = "Product Two",
                Quantity = 10,
                UnitPrice = 5m
            }
        ]
    };

    internal static Sale CreateSale(params SaleItemInput[] items)
    {
        return Sale.Create(
            "SALE-APP-001",
            SaleDate,
            CustomerId,
            "Customer One",
            BranchId,
            "Branch One",
            items.Length == 0
                ?
                [
                    new SaleItemInput(ProductOneId, "Product One", 4, 10m),
                    new SaleItemInput(ProductTwoId, "Product Two", 10, 5m)
                ]
                : items,
            new FixedTimeProvider());
    }

    internal static UpdateSaleCommand CreateUpdateCommand(Sale sale) => new()
    {
        Id = sale.Id,
        SaleNumber = "SALE-APP-UPDATED",
        SaleDate = SaleDate.AddDays(1),
        CustomerId = CustomerId,
        CustomerName = "Customer Updated",
        BranchId = BranchId,
        BranchName = "Branch Updated",
        Items =
        [
            new UpdateSaleItem
            {
                Id = sale.Items.First().Id,
                ProductId = ProductOneId,
                ProductName = "Product One Updated",
                Quantity = 5,
                UnitPrice = 12m
            },
            new UpdateSaleItem
            {
                ProductId = ProductThreeId,
                ProductName = "Product Three",
                Quantity = 3,
                UnitPrice = 7m
            }
        ]
    };

    internal static void ExecuteTransaction<TResult>(
        IUnitOfWork unitOfWork,
        CancellationToken requestCancellationToken,
        CancellationToken transactionCancellationToken)
    {
        unitOfWork.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<TResult>>>(),
                requestCancellationToken)
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<TResult>>>(0)(transactionCancellationToken));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 7, 30, 12, 30, 0, TimeSpan.Zero);
    }
}
