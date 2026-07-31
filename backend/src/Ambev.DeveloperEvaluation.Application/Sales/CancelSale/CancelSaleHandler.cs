using Ambev.DeveloperEvaluation.Domain.Repositories;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSale;

public sealed class CancelSaleHandler : IRequestHandler<CancelSaleCommand, CancelSaleResult>
{
    private readonly ISaleRepository _saleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelSaleHandler(ISaleRepository saleRepository, IUnitOfWork unitOfWork)
    {
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public Task<CancelSaleResult> Handle(CancelSaleCommand command, CancellationToken cancellationToken)
    {
        return _unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var sale = await _saleRepository.GetByIdForUpdateAsync(
                command.Id,
                transactionCancellationToken)
                ?? throw new KeyNotFoundException($"Sale with ID {command.Id} not found.");

            sale.Cancel();
            return new CancelSaleResult(true);
        }, cancellationToken);
    }
}
