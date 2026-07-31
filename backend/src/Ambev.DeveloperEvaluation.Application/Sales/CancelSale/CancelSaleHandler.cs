using Ambev.DeveloperEvaluation.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSale;

public sealed class CancelSaleHandler : IRequestHandler<CancelSaleCommand, CancelSaleResult>
{
    private readonly ISaleRepository _saleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CancelSaleHandler> _logger;

    public CancelSaleHandler(ISaleRepository saleRepository, IUnitOfWork unitOfWork)
        : this(saleRepository, unitOfWork, NullLogger<CancelSaleHandler>.Instance)
    {
    }

    public CancelSaleHandler(
        ISaleRepository saleRepository,
        IUnitOfWork unitOfWork,
        ILogger<CancelSaleHandler> logger)
    {
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CancelSaleResult> Handle(CancelSaleCommand command, CancellationToken cancellationToken)
    {
        var result = await _unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var sale = await _saleRepository.GetByIdForUpdateAsync(
                command.Id,
                transactionCancellationToken)
                ?? throw new KeyNotFoundException($"Sale with ID {command.Id} not found.");

            sale.Cancel();
            return new CancelSaleResult(true);
        }, cancellationToken);

        _logger.LogInformation(
            "Sales command {Operation} completed for sale {SaleId}",
            "CancelSale",
            command.Id);

        return result;
    }
}
