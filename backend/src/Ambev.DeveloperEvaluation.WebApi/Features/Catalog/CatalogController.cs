using Ambev.DeveloperEvaluation.Application.Catalog.SearchCatalog;
using Ambev.DeveloperEvaluation.WebApi.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Catalog;

[ApiController]
[Authorize]
[Route("api/catalog")]
public sealed class CatalogController(IMediator mediator) : BaseController
{
    [HttpGet("customers")]
    [StrictQueryParameters("search", "limit")]
    [ProducesResponseType(typeof(ApiResponseWithData<IReadOnlyList<CatalogItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> Customers([FromQuery] CatalogRequest request, CancellationToken cancellationToken) =>
        SearchAsync(CatalogType.Customer, request, "Customers retrieved successfully", cancellationToken);

    [HttpGet("branches")]
    [StrictQueryParameters("search", "limit")]
    [ProducesResponseType(typeof(ApiResponseWithData<IReadOnlyList<CatalogItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> Branches([FromQuery] CatalogRequest request, CancellationToken cancellationToken) =>
        SearchAsync(CatalogType.Branch, request, "Branches retrieved successfully", cancellationToken);

    [HttpGet("products")]
    [StrictQueryParameters("search", "limit")]
    [ProducesResponseType(typeof(ApiResponseWithData<IReadOnlyList<CatalogItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> Products([FromQuery] CatalogRequest request, CancellationToken cancellationToken) =>
        SearchAsync(CatalogType.Product, request, "Products retrieved successfully", cancellationToken);

    private async Task<IActionResult> SearchAsync(
        CatalogType type,
        CatalogRequest request,
        string message,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new SearchCatalogQuery(type, request.Search, request.Limit),
            cancellationToken);
        var items = result.Select(item => new CatalogItemResponse(item.Id, item.Name)).ToList();

        return new OkObjectResult(new ApiResponseWithData<IReadOnlyList<CatalogItemResponse>>
        {
            Success = true,
            Message = message,
            Data = items
        });
    }
}

public sealed record CatalogItemResponse(Guid Id, string Name);
