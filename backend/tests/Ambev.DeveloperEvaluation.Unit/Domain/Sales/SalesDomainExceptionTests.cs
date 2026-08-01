using Ambev.DeveloperEvaluation.Domain.Sales.Exceptions;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Sales;

public sealed class SalesDomainExceptionTests
{
    [Fact]
    public void DuplicateSaleNumberException_MessageAndCause_PreserveConflictingNumberAndOriginalFailure()
    {
        // Arrange
        var cause = new InvalidOperationException("Unique constraint violation");

        // Act
        var exception = new DuplicateSaleNumberException("SALE-001", cause);

        // Assert
        exception.Should().BeAssignableTo<SalesDomainException>();
        exception.Message.Should().Be("A sale with number 'SALE-001' already exists.");
        exception.InnerException.Should().BeSameAs(cause);
    }

    [Fact]
    public void MonetaryValueOutOfRangeException_MessageAndCause_DescribeStorageConstraintAndPreserveOriginalFailure()
    {
        // Arrange
        var cause = new OverflowException("Decimal arithmetic overflow");

        // Act
        var exception = new MonetaryValueOutOfRangeException("sale totals", cause);

        // Assert
        exception.Should().BeAssignableTo<SalesDomainException>();
        exception.Message.Should().Be("The monetary value 'sale totals' exceeds the numeric(18,2) range.");
        exception.InnerException.Should().BeSameAs(cause);
    }
}
