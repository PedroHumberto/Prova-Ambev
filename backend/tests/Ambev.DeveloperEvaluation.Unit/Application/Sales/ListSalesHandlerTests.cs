using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Sales.Enums;
using Ambev.DeveloperEvaluation.Unit.Application.Sales.TestData;
using AutoMapper;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public sealed class ListSalesHandlerTests
{
    private readonly ISaleRepository _saleRepository = Substitute.For<ISaleRepository>();
    private readonly IMapper _mapper = Substitute.For<IMapper>();

    [Fact]
    public async Task Handle_PartialLastPage_ReturnsItemsAndCorrectMetadata()
    {
        var sales = new[] { ApplicationSaleTestData.CreateSale(), ApplicationSaleTestData.CreateSale() };
        var query = new ListSalesQuery { PageNumber = 3, PageSize = 2 };
        using var cancellationSource = new CancellationTokenSource();
        var mappedItems = new List<ListSalesItemResult> { new(), new() };
        _saleRepository.GetPageAsync(
                Arg.Is<SaleQueryCriteria>(criteria => criteria.PageNumber == 3 && criteria.PageSize == 2),
                cancellationSource.Token)
            .Returns(new SalePage(sales, 5));
        _mapper.Map<List<ListSalesItemResult>>(sales).Returns(mappedItems);
        var handler = new ListSalesHandler(_saleRepository, _mapper);

        var result = await handler.Handle(query, cancellationSource.Token);

        result.Items.Should().BeSameAs(mappedItems);
        result.PageNumber.Should().Be(3);
        result.PageSize.Should().Be(2);
        result.TotalCount.Should().Be(5);
        result.TotalPages.Should().Be(3);
        await _saleRepository.Received(1).GetPageAsync(
            Arg.Is<SaleQueryCriteria>(criteria => criteria.PageNumber == 3 && criteria.PageSize == 2),
            cancellationSource.Token);
    }

    [Fact]
    public async Task Handle_EmptyPage_ReturnsZeroTotalPages()
    {
        var query = new ListSalesQuery();
        _saleRepository.GetPageAsync(
                Arg.Is<SaleQueryCriteria>(criteria =>
                    criteria.PageNumber == 1
                    && criteria.PageSize == 10
                    && criteria.Order.Count == 0),
                CancellationToken.None)
            .Returns(new SalePage([], 0));
        _mapper.Map<List<ListSalesItemResult>>(Arg.Any<object>()).Returns([]);
        var handler = new ListSalesHandler(_saleRepository, _mapper);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.PageNumber.Should().Be(ListSalesQuery.DefaultPageNumber);
        result.PageSize.Should().Be(ListSalesQuery.DefaultPageSize);
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task Handle_AllCriteria_PropagatesNormalizedCriteriaAndExactCancellationToken()
    {
        var from = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 7, 31, 23, 59, 59, DateTimeKind.Utc);
        var customerId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var branchId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        SaleSortClause[] order =
        [
            new(SaleSortField.CustomerName, SortDirection.Descending),
            new(SaleSortField.Id, SortDirection.Ascending)
        ];
        var query = new ListSalesQuery
        {
            PageNumber = 2,
            PageSize = 25,
            SaleNumber = "  SALE-001  ",
            SaleDateFrom = from,
            SaleDateTo = to,
            CustomerId = customerId,
            CustomerName = "  Customer  ",
            BranchId = branchId,
            BranchName = "  Branch  ",
            Status = SaleStatus.Cancelled,
            Order = order
        };
        using var cancellationSource = new CancellationTokenSource();
        SaleQueryCriteria? capturedCriteria = null;
        _saleRepository.GetPageAsync(
                Arg.Do<SaleQueryCriteria>(criteria => capturedCriteria = criteria),
                cancellationSource.Token)
            .Returns(new SalePage([], 26));
        _mapper.Map<List<ListSalesItemResult>>(Arg.Any<object>()).Returns([]);
        var handler = new ListSalesHandler(_saleRepository, _mapper);

        var result = await handler.Handle(query, cancellationSource.Token);

        capturedCriteria.Should().NotBeNull();
        capturedCriteria.Should().BeEquivalentTo(new SaleQueryCriteria
        {
            PageNumber = 2,
            PageSize = 25,
            SaleNumber = "SALE-001",
            SaleDateFrom = from,
            SaleDateTo = to,
            CustomerId = customerId,
            CustomerName = "Customer",
            BranchId = branchId,
            BranchName = "Branch",
            Status = SaleStatus.Cancelled,
            Order = order
        });
        capturedCriteria!.Order.Should().BeSameAs(order);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(25);
        result.TotalCount.Should().Be(26);
        result.TotalPages.Should().Be(2);
        await _saleRepository.Received(1).GetPageAsync(Arg.Any<SaleQueryCriteria>(), cancellationSource.Token);
    }
}
