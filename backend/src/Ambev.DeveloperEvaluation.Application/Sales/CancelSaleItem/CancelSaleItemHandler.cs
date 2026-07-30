using Ambev.DeveloperEvaluation.Domain.Repositories;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;

public sealed class CancelSaleItemHandler : IRequestHandler<CancelSaleItemCommand, CancelSaleItemResult>
{
    private readonly ISaleRepository _saleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelSaleItemHandler(ISaleRepository saleRepository, IUnitOfWork unitOfWork)
    {
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public Task<CancelSaleItemResult> Handle(
        CancelSaleItemCommand command,
        CancellationToken cancellationToken)
    {
        return _unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var sale = await _saleRepository.GetByIdForUpdateAsync(
                command.SaleId,
                transactionCancellationToken)
                ?? throw new KeyNotFoundException($"Sale with ID {command.SaleId} not found.");

            sale.CancelItem(command.SaleItemId);
            return new CancelSaleItemResult(true);
        }, cancellationToken);
    }
}
