using System.ComponentModel.DataAnnotations;

namespace Ambev.DeveloperEvaluation.IoC.Messaging;

public sealed class SalesMessagingOptions
{
    public const string SectionName = "SalesMessaging";

    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    [Range(1, 1000)]
    public int OutboxBatchSize { get; init; } = 20;

    [Range(100, 60000)]
    public int PollingIntervalMilliseconds { get; init; } = 1000;

    [Range(5, 3600)]
    public int LeaseSeconds { get; init; } = 60;

    [Range(1, 3600)]
    public int InitialRetryDelaySeconds { get; init; } = 5;

    [Range(1, 86400)]
    public int MaximumRetryDelaySeconds { get; init; } = 300;
}
