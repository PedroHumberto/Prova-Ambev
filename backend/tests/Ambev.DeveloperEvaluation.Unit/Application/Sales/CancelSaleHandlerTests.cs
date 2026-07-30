using Ambev.DeveloperEvaluation.Application.Sales.CancelSale;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Ambev.DeveloperEvaluation.Domain.Sales.Enums;
using Ambev.DeveloperEvaluation.Domain.Sales.Events;
using Ambev.DeveloperEvaluation.Unit.Application.Sales.TestData;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public sealed class CancelSaleHandlerTests
{
    private readonly ISaleRepository _saleRepository = Substitute.For<ISaleRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Handle_ActiveSale_CancelsUnderLockInsideTransaction()
    {
        var sale = ApplicationSaleTestData.CreateSale();
        var command = new CancelSaleCommand(sale.Id);
        using var requestSource = new CancellationTokenSource();
        using var transactionSource = new CancellationTokenSource();
        ApplicationSaleTestData.ExecuteTransaction<CancelSaleResult>(
            _unitOfWork,
            requestSource.Token,
            transactionSource.Token);
        _saleRepository.GetByIdForUpdateAsync(sale.Id, transactionSource.Token).Returns(sale);
        var handler = new CancelSaleHandler(_saleRepository, _unitOfWork);

        var result = await handler.Handle(command, requestSource.Token);

        result.Success.Should().BeTrue();
        sale.Status.Should().Be(SaleStatus.Cancelled);
        sale.CancelledAt.Should().NotBeNull();
        sale.DomainEvents.Should().ContainSingle(@event => @event is SaleCancelled);
        await _saleRepository.Received(1).GetByIdForUpdateAsync(sale.Id, transactionSource.Token);
        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task<CancelSaleResult>>>(),
            requestSource.Token);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyCancelledSale_SucceedsIdempotentlyWithoutDuplicateEvent()
    {
        var sale = ApplicationSaleTestData.CreateSale();
        sale.Cancel();
        var eventCount = sale.DomainEvents.Count;
        ApplicationSaleTestData.ExecuteTransaction<CancelSaleResult>(
            _unitOfWork,
            CancellationToken.None,
            CancellationToken.None);
        _saleRepository.GetByIdForUpdateAsync(sale.Id, CancellationToken.None).Returns(sale);
        var handler = new CancelSaleHandler(_saleRepository, _unitOfWork);

        var result = await handler.Handle(new CancelSaleCommand(sale.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        sale.DomainEvents.Should().HaveCount(eventCount);
    }

    [Fact]
    public async Task Handle_UnknownSale_ThrowsWithoutCommitOutsideTransaction()
    {
        var command = new CancelSaleCommand(ApplicationSaleTestData.SaleId);
        ApplicationSaleTestData.ExecuteTransaction<CancelSaleResult>(
            _unitOfWork,
            CancellationToken.None,
            CancellationToken.None);
        _saleRepository.GetByIdForUpdateAsync(command.Id, CancellationToken.None).Returns((Sale?)null);
        var handler = new CancelSaleHandler(_saleRepository, _unitOfWork);

        var action = () => handler.Handle(command, CancellationToken.None);

        await action.Should().ThrowAsync<KeyNotFoundException>().WithMessage($"*{command.Id}*");
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
