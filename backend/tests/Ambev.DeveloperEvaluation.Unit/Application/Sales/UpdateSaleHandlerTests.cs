using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Ambev.DeveloperEvaluation.Domain.Sales.Enums;
using Ambev.DeveloperEvaluation.Domain.Sales.Exceptions;
using Ambev.DeveloperEvaluation.Unit.Application.Sales.TestData;
using Ambev.DeveloperEvaluation.Unit.TestInfrastructure;
using AutoMapper;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public sealed class UpdateSaleHandlerTests
{
    private readonly ISaleRepository _saleRepository = Substitute.For<ISaleRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IMapper _mapper = Substitute.For<IMapper>();

    [Fact]
    public async Task Handle_ValidFullReplacement_LocksThenAtomicallyReplacesEditableData()
    {
        var sale = ApplicationSaleTestData.CreateSale();
        var omittedItem = sale.Items.Last();
        var command = ApplicationSaleTestData.CreateUpdateCommand(sale);
        using var requestSource = new CancellationTokenSource();
        using var transactionSource = new CancellationTokenSource();
        var expectedResult = new UpdateSaleResult();
        ApplicationSaleTestData.ExecuteTransaction<UpdateSaleResult>(
            _unitOfWork,
            requestSource.Token,
            transactionSource.Token);
        _saleRepository.GetByIdForUpdateAsync(sale.Id, transactionSource.Token)
            .Returns(_ =>
            {
                sale.SaleNumber.Should().Be("SALE-APP-001", "the sale must be loaded before mutation");
                return sale;
            });
        _mapper.Map<UpdateSaleResult>(sale).Returns(_ =>
        {
            sale.SaleNumber.Should().Be(command.SaleNumber, "mapping must happen after replacement");
            return expectedResult;
        });
        var logger = new RecordingLogger<UpdateSaleHandler>();
        var handler = new UpdateSaleHandler(_saleRepository, _unitOfWork, _mapper, logger);

        var result = await handler.Handle(command, requestSource.Token);

        result.Should().BeSameAs(expectedResult);
        sale.CustomerName.Should().Be("Customer Updated");
        sale.Items.Should().HaveCount(3);
        sale.Items.Single(item => item.Id == omittedItem.Id).Status.Should().Be(SaleStatus.Cancelled);
        sale.Items.Single(item => item.ProductId == ApplicationSaleTestData.ProductOneId)
            .Should().Match<SaleItem>(item => item.Quantity == 5 && item.UnitPrice == 12m);
        sale.Items.Single(item => item.ProductId == ApplicationSaleTestData.ProductThreeId).IsActive.Should().BeTrue();
        sale.TotalAmount.Should().Be(75m);
        await _saleRepository.Received(1).GetByIdForUpdateAsync(sale.Id, transactionSource.Token);
        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task<UpdateSaleResult>>>(),
            requestSource.Token);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        var log = logger.Entries.Should().ContainSingle().Subject;
        log.Properties.Keys.Should().BeEquivalentTo("Operation", "SaleId", "{OriginalFormat}");
        log.Properties["Operation"].Should().Be("UpdateSale");
        log.Properties["SaleId"].Should().Be(command.Id);
    }

    [Fact]
    public async Task Handle_UnknownSale_ThrowsWithoutMappingOrCommitOutsideTransaction()
    {
        var sale = ApplicationSaleTestData.CreateSale();
        var command = ApplicationSaleTestData.CreateUpdateCommand(sale);
        ApplicationSaleTestData.ExecuteTransaction<UpdateSaleResult>(
            _unitOfWork,
            CancellationToken.None,
            CancellationToken.None);
        _saleRepository.GetByIdForUpdateAsync(command.Id, CancellationToken.None).Returns((Sale?)null);
        var logger = new RecordingLogger<UpdateSaleHandler>();
        var handler = new UpdateSaleHandler(_saleRepository, _unitOfWork, _mapper, logger);

        var action = () => handler.Handle(command, CancellationToken.None);

        await action.Should().ThrowAsync<KeyNotFoundException>().WithMessage($"*{command.Id}*");
        _mapper.DidNotReceive().Map<UpdateSaleResult>(Arg.Any<Sale>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_TransactionCancellation_PropagatesWithoutLoggingSuccess()
    {
        var sale = ApplicationSaleTestData.CreateSale();
        var command = ApplicationSaleTestData.CreateUpdateCommand(sale);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        _unitOfWork.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<UpdateSaleResult>>>(),
                cancellationSource.Token)
            .Returns(Task.FromCanceled<UpdateSaleResult>(cancellationSource.Token));
        var logger = new RecordingLogger<UpdateSaleHandler>();
        var handler = new UpdateSaleHandler(_saleRepository, _unitOfWork, _mapper, logger);

        var action = () => handler.Handle(command, cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_InvalidReplacement_PropagatesDomainFailureWithoutPartialMutationOrMapping()
    {
        var sale = ApplicationSaleTestData.CreateSale();
        var originalSaleNumber = sale.SaleNumber;
        var command = ApplicationSaleTestData.CreateUpdateCommand(sale) with
        {
            Items =
            [
                new UpdateSaleItem
                {
                    Id = sale.Items.First().Id,
                    ProductId = ApplicationSaleTestData.ProductTwoId,
                    ProductName = "Changed product",
                    Quantity = 2,
                    UnitPrice = 5m
                }
            ]
        };
        ApplicationSaleTestData.ExecuteTransaction<UpdateSaleResult>(
            _unitOfWork,
            CancellationToken.None,
            CancellationToken.None);
        _saleRepository.GetByIdForUpdateAsync(command.Id, CancellationToken.None).Returns(sale);
        var handler = new UpdateSaleHandler(_saleRepository, _unitOfWork, _mapper);

        var action = () => handler.Handle(command, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidSaleItemException>();
        sale.SaleNumber.Should().Be(originalSaleNumber);
        sale.Items.Should().OnlyContain(item => item.IsActive);
        _mapper.DidNotReceive().Map<UpdateSaleResult>(Arg.Any<Sale>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
