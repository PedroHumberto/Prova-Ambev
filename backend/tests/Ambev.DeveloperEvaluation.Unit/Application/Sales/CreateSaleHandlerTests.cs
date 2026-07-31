using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Ambev.DeveloperEvaluation.Unit.Application.Sales.TestData;
using AutoMapper;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public sealed class CreateSaleHandlerTests
{
    private readonly ISaleRepository _saleRepository = Substitute.For<ISaleRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IMapper _mapper = Substitute.For<IMapper>();

    [Fact]
    public async Task Handle_ValidCommand_CreatesMappedSaleInsideTransaction()
    {
        var command = ApplicationSaleTestData.CreateCommand();
        using var requestSource = new CancellationTokenSource();
        using var transactionSource = new CancellationTokenSource();
        var expectedResult = new CreateSaleResult();
        Sale? addedSale = null;
        ApplicationSaleTestData.ExecuteTransaction<CreateSaleResult>(
            _unitOfWork,
            requestSource.Token,
            transactionSource.Token);
        _saleRepository.AddAsync(Arg.Do<Sale>(sale => addedSale = sale), transactionSource.Token)
            .Returns(Task.CompletedTask);
        _mapper.Map<CreateSaleResult>(Arg.Any<Sale>()).Returns(expectedResult);
        var handler = new CreateSaleHandler(_saleRepository, _unitOfWork, _mapper);

        var result = await handler.Handle(command, requestSource.Token);

        result.Should().BeSameAs(expectedResult);
        addedSale.Should().NotBeNull();
        addedSale!.SaleNumber.Should().Be(command.SaleNumber);
        addedSale.Items.Should().HaveCount(2);
        addedSale.Subtotal.Should().Be(90m);
        addedSale.DiscountAmount.Should().Be(14m);
        addedSale.TotalAmount.Should().Be(76m);
        _mapper.Received(1).Map<CreateSaleResult>(addedSale);
        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task<CreateSaleResult>>>(),
            requestSource.Token);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RepositoryFailure_PropagatesAndDoesNotMapOrCommitOutsideTransaction()
    {
        var command = ApplicationSaleTestData.CreateCommand();
        var expectedException = new InvalidOperationException("Add failed.");
        ApplicationSaleTestData.ExecuteTransaction<CreateSaleResult>(
            _unitOfWork,
            CancellationToken.None,
            CancellationToken.None);
        _saleRepository.AddAsync(Arg.Any<Sale>(), CancellationToken.None)
            .Returns(Task.FromException(expectedException));
        var handler = new CreateSaleHandler(_saleRepository, _unitOfWork, _mapper);

        var action = () => handler.Handle(command, CancellationToken.None);

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Should().BeSameAs(expectedException);
        _mapper.DidNotReceive().Map<CreateSaleResult>(Arg.Any<Sale>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
