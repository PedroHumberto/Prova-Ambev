using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.Domain.Repositories;
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
        _saleRepository.GetPageAsync(3, 2, cancellationSource.Token)
            .Returns(new SalePage(sales, 5));
        _mapper.Map<List<ListSalesItemResult>>(sales).Returns(mappedItems);
        var handler = new ListSalesHandler(_saleRepository, _mapper);

        var result = await handler.Handle(query, cancellationSource.Token);

        result.Items.Should().BeSameAs(mappedItems);
        result.PageNumber.Should().Be(3);
        result.PageSize.Should().Be(2);
        result.TotalCount.Should().Be(5);
        result.TotalPages.Should().Be(3);
        await _saleRepository.Received(1).GetPageAsync(3, 2, cancellationSource.Token);
    }

    [Fact]
    public async Task Handle_EmptyPage_ReturnsZeroTotalPages()
    {
        var query = new ListSalesQuery();
        _saleRepository.GetPageAsync(1, 10, CancellationToken.None)
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
}
