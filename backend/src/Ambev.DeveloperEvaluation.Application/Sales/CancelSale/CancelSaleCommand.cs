using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSale;

public sealed record CancelSaleCommand(Guid Id) : IRequest<CancelSaleResult>;
