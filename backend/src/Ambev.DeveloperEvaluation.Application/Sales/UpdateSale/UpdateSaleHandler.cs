using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;

public sealed class UpdateSaleHandler : IRequestHandler<UpdateSaleCommand, UpdateSaleResult>
{
    private readonly ISaleRepository _saleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<UpdateSaleHandler> _logger;

    public UpdateSaleHandler(ISaleRepository saleRepository, IUnitOfWork unitOfWork, IMapper mapper)
        : this(saleRepository, unitOfWork, mapper, NullLogger<UpdateSaleHandler>.Instance)
    {
    }

    public UpdateSaleHandler(
        ISaleRepository saleRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<UpdateSaleHandler> logger)
    {
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UpdateSaleResult> Handle(UpdateSaleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = await _unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var sale = await _saleRepository.GetByIdForUpdateAsync(
                command.Id,
                transactionCancellationToken)
                ?? throw new KeyNotFoundException($"Sale with ID {command.Id} not found.");

            sale.ReplaceEditableData(
                command.SaleNumber,
                command.SaleDate,
                command.CustomerId,
                command.CustomerName,
                command.BranchId,
                command.BranchName,
                command.Items.Select(item => new SaleItemReplacement(
                    item.Id,
                    item.ProductId,
                    item.ProductName,
                    item.Quantity,
                    item.UnitPrice)));

            return _mapper.Map<UpdateSaleResult>(sale);
        }, cancellationToken);

        _logger.LogInformation(
            "Sales command {Operation} completed for sale {SaleId}",
            "UpdateSale",
            command.Id);

        return result;
    }
}
