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
        var page = await _saleRepository.GetPageAsync(
            query.PageNumber,
            query.PageSize,
            cancellationToken);

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
