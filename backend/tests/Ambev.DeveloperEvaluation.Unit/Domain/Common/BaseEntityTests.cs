using Ambev.DeveloperEvaluation.Domain.Common;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Common;

public sealed class BaseEntityTests
{
    [Fact]
    public void CompareTo_NullEntity_ReturnsAfterNull()
    {
        // Arrange
        var entity = new BaseEntity { Id = Guid.Parse("11111111-1111-1111-1111-111111111111") };

        // Act
        var comparison = entity.CompareTo(null);

        // Assert
        comparison.Should().BePositive();
    }

    [Fact]
    public void CompareTo_EntitiesWithDifferentIds_OrdersByIdDescending()
    {
        // Arrange
        var lowerId = new BaseEntity { Id = Guid.Parse("11111111-1111-1111-1111-111111111111") };
        var higherId = new BaseEntity { Id = Guid.Parse("22222222-2222-2222-2222-222222222222") };

        // Act
        var lowerComparedToHigher = lowerId.CompareTo(higherId);
        var higherComparedToLower = higherId.CompareTo(lowerId);

        // Assert
        lowerComparedToHigher.Should().BePositive();
        higherComparedToLower.Should().BeNegative();
    }
}
