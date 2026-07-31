using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Events;

public sealed class UserRegisteredEventTests
{
    [Fact]
    public void Constructor_RegisteredUser_ExposesSameUserAsEventPayload()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Username = "registered.user",
            Email = "registered.user@example.com"
        };

        // Act
        var domainEvent = new UserRegisteredEvent(user);

        // Assert
        domainEvent.User.Should().BeSameAs(user);
    }
}
