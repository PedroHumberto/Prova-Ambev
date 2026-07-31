using Ambev.DeveloperEvaluation.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;

public sealed class CancelSaleItemHandler : IRequestHandler<CancelSaleItemCommand, CancelSaleItemResult>
{
    private readonly ISaleRepository _saleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CancelSaleItemHandler> _logger;

    public CancelSaleItemHandler(ISaleRepository saleRepository, IUnitOfWork unitOfWork)
        : this(saleRepository, unitOfWork, NullLogger<CancelSaleItemHandler>.Instance)
    {
    }

    public CancelSaleItemHandler(
        ISaleRepository saleRepository,
        IUnitOfWork unitOfWork,
        ILogger<CancelSaleItemHandler> logger)
    {
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CancelSaleItemResult> Handle(
        CancelSaleItemCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var sale = await _saleRepository.GetByIdForUpdateAsync(
                command.SaleId,
                transactionCancellationToken)
                ?? throw new KeyNotFoundException($"Sale with ID {command.SaleId} not found.");

            sale.CancelItem(command.SaleItemId);
            return new CancelSaleItemResult(true);
        }, cancellationToken);

        _logger.LogInformation(
            "Sales command {Operation} completed for sale {SaleId} and item {SaleItemId}",
            "CancelSaleItem",
            command.SaleId,
            command.SaleItemId);

        return result;
    }
}
