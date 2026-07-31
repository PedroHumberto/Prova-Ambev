using Ambev.DeveloperEvaluation.IoC.HealthChecks;
using Ambev.DeveloperEvaluation.IoC.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.IoC;

public sealed class RabbitMqHealthCheckTests
{
    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new RabbitMqHealthCheck(null!));

        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public async Task CheckHealthAsync_MalformedConnectionString_ReturnsUnhealthyWithoutConnecting()
    {
        var healthCheck = new RabbitMqHealthCheck(Options.Create(new SalesMessagingOptions
        {
            ConnectionString = "not an absolute RabbitMQ URI"
        }));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("RabbitMQ is unavailable.", result.Description);
    }
}
