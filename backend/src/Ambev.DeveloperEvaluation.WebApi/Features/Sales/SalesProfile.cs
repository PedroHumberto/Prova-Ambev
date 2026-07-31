using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Application.Sales.GetSaleById;
using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using AutoMapper;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

public sealed class SalesProfile : Profile
{
    public SalesProfile()
    {
        CreateMap<CreateSaleRequest, CreateSaleCommand>();
        CreateMap<CreateSaleItemRequest, CreateSaleItem>();
        CreateMap<UpdateSaleRequest, UpdateSaleCommand>()
            .ForMember(destination => destination.Id, options => options.Ignore());
        CreateMap<UpdateSaleItemRequest, UpdateSaleItem>();

        CreateMap<SaleItemResult, SaleItemResponse>()
            .ForMember(destination => destination.Status, options => options.MapFrom(source => source.Status.ToString()));
        CreateMap<CreateSaleResult, SaleResponse>()
            .ForMember(destination => destination.Status, options => options.MapFrom(source => source.Status.ToString()));
        CreateMap<GetSaleByIdResult, SaleResponse>()
            .ForMember(destination => destination.Status, options => options.MapFrom(source => source.Status.ToString()));
        CreateMap<UpdateSaleResult, SaleResponse>()
            .ForMember(destination => destination.Status, options => options.MapFrom(source => source.Status.ToString()));
        CreateMap<ListSalesItemResult, SaleSummaryResponse>()
            .ForMember(destination => destination.Status, options => options.MapFrom(source => source.Status.ToString()));
        CreateMap<ListSalesResult, ListSalesResponse>();
    }
}
