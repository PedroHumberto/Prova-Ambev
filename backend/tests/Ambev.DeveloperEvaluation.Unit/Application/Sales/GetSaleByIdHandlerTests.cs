using Ambev.DeveloperEvaluation.Application.Sales.GetSaleById;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Ambev.DeveloperEvaluation.Unit.Application.Sales.TestData;
using AutoMapper;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public sealed class GetSaleByIdHandlerTests
{
    private readonly ISaleRepository _saleRepository = Substitute.For<ISaleRepository>();
    private readonly IMapper _mapper = Substitute.For<IMapper>();

    [Theory]
    [InlineData("saleRepository")]
    [InlineData("mapper")]
    public void Constructor_MissingRequiredDependency_ThrowsArgumentNullException(string dependencyName)
    {
        Action action = dependencyName == "saleRepository"
            ? () => new GetSaleByIdHandler(null!, _mapper)
            : () => new GetSaleByIdHandler(_saleRepository, null!);

        action.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be(dependencyName);
    }

    [Fact]
    public async Task Handle_ExistingSale_ReturnsMappedResultAndPropagatesToken()
    {
        var sale = ApplicationSaleTestData.CreateSale();
        var query = new GetSaleByIdQuery(sale.Id);
        using var cancellationSource = new CancellationTokenSource();
        var expectedResult = new GetSaleByIdResult();
        _saleRepository.GetByIdAsync(sale.Id, cancellationSource.Token).Returns(sale);
        _mapper.Map<GetSaleByIdResult>(sale).Returns(expectedResult);
        var handler = new GetSaleByIdHandler(_saleRepository, _mapper);

        var result = await handler.Handle(query, cancellationSource.Token);

        result.Should().BeSameAs(expectedResult);
        await _saleRepository.Received(1).GetByIdAsync(sale.Id, cancellationSource.Token);
        _mapper.Received(1).Map<GetSaleByIdResult>(sale);
    }

    [Fact]
    public async Task Handle_UnknownSale_ThrowsAndDoesNotMap()
    {
        var query = new GetSaleByIdQuery(ApplicationSaleTestData.SaleId);
        _saleRepository.GetByIdAsync(query.Id, CancellationToken.None).Returns((Sale?)null);
        var handler = new GetSaleByIdHandler(_saleRepository, _mapper);

        var action = () => handler.Handle(query, CancellationToken.None);

        await action.Should().ThrowAsync<KeyNotFoundException>().WithMessage($"*{query.Id}*");
        _mapper.DidNotReceive().Map<GetSaleByIdResult>(Arg.Any<Sale>());
    }
}
