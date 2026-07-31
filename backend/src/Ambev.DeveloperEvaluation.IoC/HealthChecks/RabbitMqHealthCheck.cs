using Ambev.DeveloperEvaluation.IoC.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Ambev.DeveloperEvaluation.IoC.HealthChecks;

public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);
    private readonly string _connectionString;

    public RabbitMqHealthCheck(IOptions<SalesMessagingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _connectionString = options.Value.ConnectionString;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionFactory = new ConnectionFactory
            {
                Uri = new Uri(_connectionString, UriKind.Absolute),
                RequestedConnectionTimeout = ConnectionTimeout
            };

            await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            return connection.IsOpen
                ? HealthCheckResult.Healthy("RabbitMQ is available.")
                : HealthCheckResult.Unhealthy("RabbitMQ is unavailable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ is unavailable.");
        }
    }
}
