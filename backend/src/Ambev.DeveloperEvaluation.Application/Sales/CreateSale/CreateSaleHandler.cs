using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ambev.DeveloperEvaluation.Application.Sales.CreateSale;

public sealed class CreateSaleHandler : IRequestHandler<CreateSaleCommand, CreateSaleResult>
{
    private readonly ISaleRepository _saleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<CreateSaleHandler> _logger;

    public CreateSaleHandler(ISaleRepository saleRepository, IUnitOfWork unitOfWork, IMapper mapper)
        : this(saleRepository, unitOfWork, mapper, NullLogger<CreateSaleHandler>.Instance)
    {
    }

    public CreateSaleHandler(
        ISaleRepository saleRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<CreateSaleHandler> logger)
    {
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CreateSaleResult> Handle(CreateSaleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = await _unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var sale = Sale.Create(
                command.SaleNumber,
                command.SaleDate,
                command.CustomerId,
                command.CustomerName,
                command.BranchId,
                command.BranchName,
                command.Items.Select(item => new SaleItemInput(
                    item.ProductId,
                    item.ProductName,
                    item.Quantity,
                    item.UnitPrice)));

            await _saleRepository.AddAsync(sale, transactionCancellationToken);
            return _mapper.Map<CreateSaleResult>(sale);
        }, cancellationToken);

        _logger.LogInformation(
            "Sales command {Operation} completed for sale {SaleId}",
            "CreateSale",
            result.Id);

        return result;
    }
}
