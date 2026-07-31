using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Application.Sales.GetSaleById;
using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using AutoMapper;

namespace Ambev.DeveloperEvaluation.Application.Sales.Common;

public sealed class SaleMappingProfile : Profile
{
    public SaleMappingProfile()
    {
        CreateMap<SaleItem, SaleItemResult>();
        CreateMap<Sale, CreateSaleResult>();
        CreateMap<Sale, GetSaleByIdResult>();
        CreateMap<Sale, UpdateSaleResult>();
        CreateMap<Sale, ListSalesItemResult>();
    }
}
