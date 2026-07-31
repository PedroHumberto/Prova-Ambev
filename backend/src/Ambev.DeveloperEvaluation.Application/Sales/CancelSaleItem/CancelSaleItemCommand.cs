using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;

public sealed record CancelSaleItemCommand(Guid SaleId, Guid SaleItemId) : IRequest<CancelSaleItemResult>;
