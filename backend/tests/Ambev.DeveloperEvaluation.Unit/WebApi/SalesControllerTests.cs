using Ambev.DeveloperEvaluation.Application.Sales.CancelSale;
using Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;
using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Application.Sales.GetSaleById;
using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Sales.Enums;
using Ambev.DeveloperEvaluation.WebApi.Common;
using Ambev.DeveloperEvaluation.WebApi.Features.Sales;
using AutoMapper;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.WebApi;

public sealed class SalesControllerTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IMapper _mapper = Substitute.For<IMapper>();

    [Fact]
    public async Task Create_ValidRequest_SendsCommandAndReturnsCreatedAtGetById()
    {
        var request = ValidCreateRequest();
        var command = new CreateSaleCommand { SaleNumber = request.SaleNumber };
        var result = new CreateSaleResult { Id = SaleId, SaleNumber = request.SaleNumber };
        var response = new SaleResponse { Id = SaleId, SaleNumber = request.SaleNumber };
        var cancellationToken = new CancellationTokenSource().Token;
        _mapper.Map<CreateSaleCommand>(request).Returns(command);
        _mediator.Send(command, cancellationToken).Returns(result);
        _mapper.Map<SaleResponse>(result).Returns(response);

        var actionResult = await CreateController().Create(request, cancellationToken);

        var created = actionResult.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(SalesController.GetById));
        created.RouteValues.Should().ContainKey("id").WhoseValue.Should().Be(SaleId);
        var envelope = created.Value.Should().BeOfType<ApiResponseWithData<SaleResponse>>().Subject;
        envelope.Data.Should().BeSameAs(response);
        envelope.Success.Should().BeTrue();
        await _mediator.Received(1).Send(command, cancellationToken);
    }

    [Fact]
    public async Task GetById_Request_SendsRouteIdAndCancellationToken()
    {
        var request = new SaleIdRequest { Id = SaleId };
        var result = new GetSaleByIdResult { Id = SaleId };
        var cancellationToken = new CancellationTokenSource().Token;
        _mediator.Send(Arg.Is<GetSaleByIdQuery>(query => query.Id == SaleId), cancellationToken).Returns(result);

        var actionResult = await CreateController().GetById(request, cancellationToken);

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ApiResponseWithData<SaleResponse>>();
        await _mediator.Received(1).Send(
            Arg.Is<GetSaleByIdQuery>(query => query.Id == SaleId),
            cancellationToken);
    }

    [Fact]
    public async Task List_CustomPagination_SendsPageValuesAndCancellationToken()
    {
        var request = new ListSalesRequest { PageNumber = 3, PageSize = 25 };
        var result = new ListSalesResult { PageNumber = 3, PageSize = 25 };
        var cancellationToken = new CancellationTokenSource().Token;
        _mediator.Send(
                Arg.Is<ListSalesQuery>(query => query.PageNumber == 3 && query.PageSize == 25),
                cancellationToken)
            .Returns(result);

        var actionResult = await CreateController().List(request, cancellationToken);

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ApiResponseWithData<ListSalesResponse>>();
        await _mediator.Received(1).Send(
            Arg.Is<ListSalesQuery>(query => query.PageNumber == 3 && query.PageSize == 25),
            cancellationToken);
    }

    [Fact]
    public async Task List_AllFiltersAndOrder_PropagatesCompleteQueryAndReturnsCurrentEnvelope()
    {
        var from = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 7, 31, 23, 59, 59, TimeSpan.Zero);
        var request = new ListSalesRequest
        {
            PageNumber = 4,
            PageSize = 20,
            Order = "CUSTOMERNAME DESC,totalAmount,id desc",
            SaleNumber = " SALE-001 ",
            SaleDateFrom = from,
            SaleDateTo = to,
            CustomerId = CustomerId,
            CustomerName = " Customer ",
            BranchId = BranchId,
            BranchName = " Branch ",
            Status = "Cancelled"
        };
        var result = new ListSalesResult { PageNumber = 4, PageSize = 20, TotalCount = 61, TotalPages = 4 };
        var response = new ListSalesResponse();
        using var cancellationSource = new CancellationTokenSource();
        ListSalesQuery? capturedQuery = null;
        _mediator.Send(
                Arg.Do<ListSalesQuery>(query => capturedQuery = query),
                cancellationSource.Token)
            .Returns(result);
        _mapper.Map<ListSalesResponse>(result).Returns(response);

        var actionResult = await CreateController().List(request, cancellationSource.Token);

        capturedQuery.Should().NotBeNull();
        capturedQuery.Should().BeEquivalentTo(new ListSalesQuery
        {
            PageNumber = 4,
            PageSize = 20,
            SaleNumber = request.SaleNumber,
            SaleDateFrom = from.UtcDateTime,
            SaleDateTo = to.UtcDateTime,
            CustomerId = CustomerId,
            CustomerName = request.CustomerName,
            BranchId = BranchId,
            BranchName = request.BranchName,
            Status = SaleStatus.Cancelled,
            Order = new[]
            {
                new SaleSortClause(SaleSortField.CustomerName, SortDirection.Descending),
                new SaleSortClause(SaleSortField.TotalAmount, SortDirection.Ascending),
                new SaleSortClause(SaleSortField.Id, SortDirection.Descending)
            }
        });
        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var envelope = ok.Value.Should().BeOfType<ApiResponseWithData<ListSalesResponse>>().Subject;
        envelope.Success.Should().BeTrue();
        envelope.Message.Should().Be("Sales retrieved successfully");
        envelope.Data.Should().BeSameAs(response);
        await _mediator.Received(1).Send(Arg.Any<ListSalesQuery>(), cancellationSource.Token);
    }

    [Fact]
    public async Task Update_RouteIdAndBody_SendsFullBodyCommandWithRouteId()
    {
        var route = new SaleIdRequest { Id = SaleId };
        var request = ValidUpdateRequest();
        var mappedCommand = new UpdateSaleCommand { Id = Guid.Parse("99999999-9999-9999-9999-999999999999"), SaleNumber = request.SaleNumber };
        var result = new UpdateSaleResult { Id = SaleId };
        var cancellationToken = new CancellationTokenSource().Token;
        _mapper.Map<UpdateSaleCommand>(request).Returns(mappedCommand);
        _mediator.Send(
                Arg.Is<UpdateSaleCommand>(command => command.Id == SaleId && command.SaleNumber == request.SaleNumber),
                cancellationToken)
            .Returns(result);

        var actionResult = await CreateController().Update(route, request, cancellationToken);

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ApiResponseWithData<SaleResponse>>();
        await _mediator.Received(1).Send(
            Arg.Is<UpdateSaleCommand>(command => command.Id == SaleId && command.SaleNumber == request.SaleNumber),
            cancellationToken);
    }

    [Fact]
    public async Task Cancel_Request_SendsSaleIdAndReturnsSuccessEnvelope()
    {
        var cancellationToken = new CancellationTokenSource().Token;
        _mediator.Send(Arg.Is<CancelSaleCommand>(command => command.Id == SaleId), cancellationToken)
            .Returns(new CancelSaleResult(true));

        var actionResult = await CreateController().Cancel(new SaleIdRequest { Id = SaleId }, cancellationToken);

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var envelope = ok.Value.Should().BeOfType<ApiResponse>().Subject;
        envelope.Success.Should().BeTrue();
        envelope.Message.Should().Be("Sale cancelled successfully");
        await _mediator.Received(1).Send(
            Arg.Is<CancelSaleCommand>(command => command.Id == SaleId),
            cancellationToken);
    }

    [Fact]
    public async Task CancelItem_Request_SendsBothIdsAndCancellationToken()
    {
        var request = new CancelSaleItemRequest { SaleId = SaleId, ItemId = ItemId };
        var cancellationToken = new CancellationTokenSource().Token;
        _mediator.Send(
                Arg.Is<CancelSaleItemCommand>(command =>
                    command.SaleId == SaleId && command.SaleItemId == ItemId),
                cancellationToken)
            .Returns(new CancelSaleItemResult(true));

        var actionResult = await CreateController().CancelItem(request, cancellationToken);

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ApiResponse>();
        await _mediator.Received(1).Send(
            Arg.Is<CancelSaleItemCommand>(command =>
                command.SaleId == SaleId && command.SaleItemId == ItemId),
            cancellationToken);
    }

    private SalesController CreateController() => new(_mediator, _mapper);

    private static CreateSaleRequest ValidCreateRequest() => new()
    {
        SaleNumber = "SALE-WEB-001",
        SaleDate = SaleDate,
        CustomerId = CustomerId,
        CustomerName = "Customer",
        BranchId = BranchId,
        BranchName = "Branch",
        Items =
        [
            new CreateSaleItemRequest
            {
                ProductId = ProductId,
                ProductName = "Product",
                Quantity = 4,
                UnitPrice = 10m
            }
        ]
    };

    private static UpdateSaleRequest ValidUpdateRequest() => new()
    {
        SaleNumber = "SALE-WEB-UPDATED",
        SaleDate = SaleDate,
        CustomerId = CustomerId,
        CustomerName = "Customer",
        BranchId = BranchId,
        BranchName = "Branch",
        Items =
        [
            new UpdateSaleItemRequest
            {
                Id = ItemId,
                ProductId = ProductId,
                ProductName = "Product",
                Quantity = 5,
                UnitPrice = 12m
            }
        ]
    };

    private static readonly Guid SaleId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ItemId = Guid.Parse("11000000-0000-0000-0000-000000000001");
    private static readonly Guid CustomerId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid BranchId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid ProductId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly DateTime SaleDate = new(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);
}
