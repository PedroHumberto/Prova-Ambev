using Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Ambev.DeveloperEvaluation.Domain.Sales.Enums;
using Ambev.DeveloperEvaluation.Domain.Sales.Events;
using Ambev.DeveloperEvaluation.Domain.Sales.Exceptions;
using Ambev.DeveloperEvaluation.Unit.Application.Sales.TestData;
using Ambev.DeveloperEvaluation.Unit.TestInfrastructure;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public sealed class CancelSaleItemHandlerTests
{
    private readonly ISaleRepository _saleRepository = Substitute.For<ISaleRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Handle_ActiveItem_CancelsItemUnderLockAndRecalculatesTotals()
    {
        var sale = ApplicationSaleTestData.CreateSale();
        var item = sale.Items.First();
        var command = new CancelSaleItemCommand(sale.Id, item.Id);
        using var requestSource = new CancellationTokenSource();
        using var transactionSource = new CancellationTokenSource();
        ApplicationSaleTestData.ExecuteTransaction<CancelSaleItemResult>(
            _unitOfWork,
            requestSource.Token,
            transactionSource.Token);
        _saleRepository.GetByIdForUpdateAsync(sale.Id, transactionSource.Token).Returns(sale);
        var logger = new RecordingLogger<CancelSaleItemHandler>();
        var handler = new CancelSaleItemHandler(_saleRepository, _unitOfWork, logger);

        var result = await handler.Handle(command, requestSource.Token);

        result.Success.Should().BeTrue();
        item.Status.Should().Be(SaleStatus.Cancelled);
        sale.TotalAmount.Should().Be(40m);
        sale.DomainEvents.Should().ContainSingle(@event => @event is SaleItemCancelled);
        await _saleRepository.Received(1).GetByIdForUpdateAsync(sale.Id, transactionSource.Token);
        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task<CancelSaleItemResult>>>(),
            requestSource.Token);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        var log = logger.Entries.Should().ContainSingle().Subject;
        log.Properties.Keys.Should().BeEquivalentTo("Operation", "SaleId", "SaleItemId", "{OriginalFormat}");
        log.Properties["Operation"].Should().Be("CancelSaleItem");
        log.Properties["SaleId"].Should().Be(command.SaleId);
        log.Properties["SaleItemId"].Should().Be(command.SaleItemId);
    }

    [Fact]
    public async Task Handle_AlreadyCancelledItem_SucceedsIdempotentlyWithoutDuplicateEvent()
    {
        var sale = ApplicationSaleTestData.CreateSale();
        var item = sale.Items.First();
        sale.CancelItem(item.Id);
        var eventCount = sale.DomainEvents.Count;
        ApplicationSaleTestData.ExecuteTransaction<CancelSaleItemResult>(
            _unitOfWork,
            CancellationToken.None,
            CancellationToken.None);
        _saleRepository.GetByIdForUpdateAsync(sale.Id, CancellationToken.None).Returns(sale);
        var logger = new RecordingLogger<CancelSaleItemHandler>();
        var handler = new CancelSaleItemHandler(_saleRepository, _unitOfWork, logger);

        var result = await handler.Handle(
            new CancelSaleItemCommand(sale.Id, item.Id),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        sale.DomainEvents.Should().HaveCount(eventCount);
    }

    [Fact]
    public async Task Handle_LastActiveItem_PropagatesDomainFailureWithoutCommitOutsideTransaction()
    {
        var sale = ApplicationSaleTestData.CreateSale(
            new SaleItemInput(ApplicationSaleTestData.ProductOneId, "Only product", 1, 10m));
        var item = sale.Items.Single();
        ApplicationSaleTestData.ExecuteTransaction<CancelSaleItemResult>(
            _unitOfWork,
            CancellationToken.None,
            CancellationToken.None);
        _saleRepository.GetByIdForUpdateAsync(sale.Id, CancellationToken.None).Returns(sale);
        var logger = new RecordingLogger<CancelSaleItemHandler>();
        var handler = new CancelSaleItemHandler(_saleRepository, _unitOfWork, logger);

        var action = () => handler.Handle(
            new CancelSaleItemCommand(sale.Id, item.Id),
            CancellationToken.None);

        await action.Should().ThrowAsync<LastActiveSaleItemException>();
        item.IsActive.Should().BeTrue();
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_TransactionCancellation_PropagatesWithoutLoggingSuccess()
    {
        var command = new CancelSaleItemCommand(
            ApplicationSaleTestData.SaleId,
            ApplicationSaleTestData.ProductOneId);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        _unitOfWork.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<CancelSaleItemResult>>>(),
                cancellationSource.Token)
            .Returns(Task.FromCanceled<CancelSaleItemResult>(cancellationSource.Token));
        var logger = new RecordingLogger<CancelSaleItemHandler>();
        var handler = new CancelSaleItemHandler(_saleRepository, _unitOfWork, logger);

        var action = () => handler.Handle(command, cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UnknownItem_PropagatesNotFoundFailureWithoutCommitOutsideTransaction()
    {
        var sale = ApplicationSaleTestData.CreateSale();
        var unknownItemId = Guid.Parse("50000000-0000-0000-0000-000000000001");
        ApplicationSaleTestData.ExecuteTransaction<CancelSaleItemResult>(
            _unitOfWork,
            CancellationToken.None,
            CancellationToken.None);
        _saleRepository.GetByIdForUpdateAsync(sale.Id, CancellationToken.None).Returns(sale);
        var handler = new CancelSaleItemHandler(_saleRepository, _unitOfWork);

        var action = () => handler.Handle(
            new CancelSaleItemCommand(sale.Id, unknownItemId),
            CancellationToken.None);

        await action.Should().ThrowAsync<SaleItemNotFoundException>();
        sale.Items.Should().OnlyContain(item => item.IsActive);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownSale_ThrowsWithoutCommitOutsideTransaction()
    {
        var command = new CancelSaleItemCommand(
            ApplicationSaleTestData.SaleId,
            ApplicationSaleTestData.ProductOneId);
        ApplicationSaleTestData.ExecuteTransaction<CancelSaleItemResult>(
            _unitOfWork,
            CancellationToken.None,
            CancellationToken.None);
        _saleRepository.GetByIdForUpdateAsync(command.SaleId, CancellationToken.None).Returns((Sale?)null);
        var handler = new CancelSaleItemHandler(_saleRepository, _unitOfWork);

        var action = () => handler.Handle(command, CancellationToken.None);

        await action.Should().ThrowAsync<KeyNotFoundException>().WithMessage($"*{command.SaleId}*");
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
