using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.IoC.Messaging;
using Ambev.DeveloperEvaluation.IoC.HealthChecks;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.DataSeeding;
using Ambev.DeveloperEvaluation.ORM.Outbox;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Rebus.Config;

namespace Ambev.DeveloperEvaluation.IoC.ModuleInitializers;

public class InfrastructureModuleInitializer : IModuleInitializer
{
    public void Initialize(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<DefaultContext>());
        builder.Services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DefaultContext>());
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<ISaleRepository, SaleRepository>();
        builder.Services.AddScoped<ICatalogRepository, CatalogRepository>();
        builder.Services.AddScoped<IOutboxStore, OutboxStore>();
        builder.Services.AddScoped<OutboxDispatcher>();
        builder.Services.AddScoped<DemoDataSeeder>();
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<DefaultContext>(
                "postgresql",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["readiness"])
            .AddCheck<RabbitMqHealthCheck>(
                "rabbitmq",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["readiness"],
                timeout: TimeSpan.FromSeconds(5));

        builder.Services.AddOptions<SalesMessagingOptions>()
            .Bind(builder.Configuration.GetSection(SalesMessagingOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => options.MaximumRetryDelaySeconds >= options.InitialRetryDelaySeconds,
                "MaximumRetryDelaySeconds must be greater than or equal to InitialRetryDelaySeconds.")
            .ValidateOnStart();

        builder.Services.AddRebus((configure, serviceProvider) =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<SalesMessagingOptions>>()
                .Value;

            return configure.Transport(
                transport => transport.UseRabbitMqAsOneWayClient(options.ConnectionString));
        });

        builder.Services.AddSingleton<IIntegrationEventPublisher, RebusIntegrationEventPublisher>();
        builder.Services.AddHostedService<OutboxDispatcherWorker>();
    }
}
