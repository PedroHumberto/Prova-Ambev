using Ambev.DeveloperEvaluation.Application.Sales.CancelSale;
using Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;
using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Application.Sales.GetSaleById;
using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using Ambev.DeveloperEvaluation.WebApi.Common;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

/// <summary>
/// Manages sales and item cancellation.
/// </summary>
[ApiController]
[Authorize]
[Route("api/sales")]
public sealed class SalesController(IMediator mediator, IMapper mapper) : BaseController
{
    /// <summary>Creates a sale. Discounts, totals, status, and audit fields are calculated by the server.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponseWithData<SaleResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSaleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(mapper.Map<CreateSaleCommand>(request), cancellationToken);
        var response = new ApiResponseWithData<SaleResponse>
        {
            Success = true,
            Message = "Sale created successfully",
            Data = mapper.Map<SaleResponse>(result)
        };

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, response);
    }

    /// <summary>Gets a sale by its identifier.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponseWithData<SaleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(
        [FromRoute] SaleIdRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSaleByIdQuery(request.Id), cancellationToken);
        return new OkObjectResult(new ApiResponseWithData<SaleResponse>
        {
            Success = true,
            Message = "Sale retrieved successfully",
            Data = mapper.Map<SaleResponse>(result)
        });
    }

    /// <summary>Lists sales with deterministic default ordering and basic pagination.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseWithData<ListSalesResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> List(
        [FromQuery] ListSalesRequest request,
        CancellationToken cancellationToken)
    {
        var query = new ListSalesQuery
        {
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
        var result = await mediator.Send(query, cancellationToken);
        return new OkObjectResult(new ApiResponseWithData<ListSalesResponse>
        {
            Success = true,
            Message = "Sales retrieved successfully",
            Data = mapper.Map<ListSalesResponse>(result)
        });
    }

    /// <summary>Fully replaces the editable sale data. Omitted existing items are cancelled.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponseWithData<SaleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(
        [FromRoute] SaleIdRequest route,
        [FromBody] UpdateSaleRequest request,
        CancellationToken cancellationToken)
    {
        var command = mapper.Map<UpdateSaleCommand>(request) with { Id = route.Id };
        var result = await mediator.Send(command, cancellationToken);
        return new OkObjectResult(new ApiResponseWithData<SaleResponse>
        {
            Success = true,
            Message = "Sale updated successfully",
            Data = mapper.Map<SaleResponse>(result)
        });
    }

    /// <summary>Cancels a sale. Repeated cancellation is idempotent.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Cancel(
        [FromRoute] SaleIdRequest request,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelSaleCommand(request.Id), cancellationToken);
        return new OkObjectResult(new ApiResponse { Success = true, Message = "Sale cancelled successfully" });
    }

    /// <summary>Cancels an individual sale item. Repeated cancellation is idempotent.</summary>
    [HttpDelete("{saleId}/items/{itemId}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CancelItem(
        [FromRoute] CancelSaleItemRequest request,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelSaleItemCommand(request.SaleId, request.ItemId), cancellationToken);
        return new OkObjectResult(new ApiResponse { Success = true, Message = "Sale item cancelled successfully" });
    }
}
