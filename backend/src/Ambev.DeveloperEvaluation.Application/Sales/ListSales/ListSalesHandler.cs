using Ambev.DeveloperEvaluation.Domain.Repositories;
using AutoMapper;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.ListSales;

public sealed class ListSalesHandler : IRequestHandler<ListSalesQuery, ListSalesResult>
{
    private readonly ISaleRepository _saleRepository;
    private readonly IMapper _mapper;

    public ListSalesHandler(ISaleRepository saleRepository, IMapper mapper)
    {
        _saleRepository = saleRepository ?? throw new ArgumentNullException(nameof(saleRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<ListSalesResult> Handle(ListSalesQuery query, CancellationToken cancellationToken)
    {
        var criteria = new SaleQueryCriteria
        {
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            SaleNumber = query.SaleNumber?.Trim(),
            SaleDateFrom = query.SaleDateFrom,
            SaleDateTo = query.SaleDateTo,
            CustomerId = query.CustomerId,
            CustomerName = query.CustomerName?.Trim(),
            BranchId = query.BranchId,
            BranchName = query.BranchName?.Trim(),
            Status = query.Status,
            Order = query.Order
        };
        var page = await _saleRepository.GetPageAsync(criteria, cancellationToken);

        return new ListSalesResult
        {
            Items = _mapper.Map<List<ListSalesItemResult>>(page.Items),
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = page.TotalCount,
            TotalPages = page.TotalCount == 0
                ? 0
                : (int)Math.Ceiling(page.TotalCount / (double)query.PageSize)
        };
    }
}
