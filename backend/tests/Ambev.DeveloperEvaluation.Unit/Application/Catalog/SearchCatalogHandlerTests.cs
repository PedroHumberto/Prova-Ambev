using Ambev.DeveloperEvaluation.Application.Catalog.SearchCatalog;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Catalog;

public sealed class SearchCatalogHandlerTests
{
    private readonly ICatalogRepository _repository = Substitute.For<ICatalogRepository>();

    [Fact]
    public void Constructor_MissingRepository_ThrowsArgumentNullException()
    {
        var action = () => new SearchCatalogHandler(null!);

        action.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("catalogRepository");
    }

    [Theory]
    [InlineData(CatalogType.Customer)]
    [InlineData(CatalogType.Branch)]
    [InlineData(CatalogType.Product)]
    public async Task Handle_CatalogType_UsesMatchingRepositoryQuery(CatalogType type)
    {
        var snapshot = new CatalogItemSnapshot(Guid.NewGuid(), "Snapshot name");
        using var cancellationSource = new CancellationTokenSource();
        _repository.SearchCustomersAsync("term", 7, cancellationSource.Token).Returns([snapshot]);
        _repository.SearchBranchesAsync("term", 7, cancellationSource.Token).Returns([snapshot]);
        _repository.SearchProductsAsync("term", 7, cancellationSource.Token).Returns([snapshot]);
        var handler = new SearchCatalogHandler(_repository);

        var result = await handler.Handle(
            new SearchCatalogQuery(type, "  term  ", 7),
            cancellationSource.Token);

        result.Should().Equal(new CatalogItemResult(snapshot.Id, snapshot.Name));
        switch (type)
        {
            case CatalogType.Customer:
                await _repository.Received(1).SearchCustomersAsync("term", 7, cancellationSource.Token);
                break;
            case CatalogType.Branch:
                await _repository.Received(1).SearchBranchesAsync("term", 7, cancellationSource.Token);
                break;
            case CatalogType.Product:
                await _repository.Received(1).SearchProductsAsync("term", 7, cancellationSource.Token);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    [Fact]
    public async Task Handle_BlankSearch_NormalizesSearchToNull()
    {
        _repository.SearchCustomersAsync(null, 10, CancellationToken.None).Returns([]);
        var handler = new SearchCatalogHandler(_repository);

        var result = await handler.Handle(
            new SearchCatalogQuery(CatalogType.Customer, "   "),
            CancellationToken.None);

        result.Should().BeEmpty();
        await _repository.Received(1).SearchCustomersAsync(null, 10, CancellationToken.None);
    }
}
