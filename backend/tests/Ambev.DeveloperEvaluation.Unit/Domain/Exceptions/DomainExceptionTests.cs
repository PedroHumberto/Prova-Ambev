using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Exceptions;

public sealed class DomainExceptionTests
{
    [Fact]
    public void Constructor_MessageOnly_ExposesDomainMessage()
    {
        // Act
        var exception = new global::DomainException("Domain rule was violated.");

        // Assert
        exception.Message.Should().Be("Domain rule was violated.");
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_MessageAndCause_PreservesOriginalCause()
    {
        // Arrange
        var cause = new InvalidOperationException("Original cause");

        // Act
        var exception = new global::DomainException("Domain operation failed.", cause);

        // Assert
        exception.Message.Should().Be("Domain operation failed.");
        exception.InnerException.Should().BeSameAs(cause);
    }
}
