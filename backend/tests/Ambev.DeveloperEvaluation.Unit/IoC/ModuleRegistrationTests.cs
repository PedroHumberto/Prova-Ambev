using Ambev.DeveloperEvaluation.Common.Security;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.IoC;
using Ambev.DeveloperEvaluation.IoC.HealthChecks;
using Ambev.DeveloperEvaluation.IoC.Messaging;
using Ambev.DeveloperEvaluation.IoC.ModuleInitializers;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Outbox;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.IoC;

public sealed class ModuleRegistrationTests
{
    private static readonly Dictionary<string, string?> ValidMessagingConfiguration = new()
    {
        [$"{SalesMessagingOptions.SectionName}:ConnectionString"] = "amqp://guest:guest@localhost:5672",
        [$"{SalesMessagingOptions.SectionName}:OutboxBatchSize"] = "25",
        [$"{SalesMessagingOptions.SectionName}:PollingIntervalMilliseconds"] = "500",
        [$"{SalesMessagingOptions.SectionName}:LeaseSeconds"] = "45",
        [$"{SalesMessagingOptions.SectionName}:InitialRetryDelaySeconds"] = "10",
        [$"{SalesMessagingOptions.SectionName}:MaximumRetryDelaySeconds"] = "120"
    };

    [Fact]
    public void Initialize_DefaultBuilder_RegistersInfrastructureServicesWithExpectedLifetimes()
    {
        var builder = CreateBuilder();

        new InfrastructureModuleInitializer().Initialize(builder);

        AssertRegistration<DbContext>(builder.Services, ServiceLifetime.Scoped);
        AssertRegistration<IUnitOfWork>(builder.Services, ServiceLifetime.Scoped);
        AssertRegistration<IUserRepository, UserRepository>(builder.Services, ServiceLifetime.Scoped);
        AssertRegistration<ISaleRepository, SaleRepository>(builder.Services, ServiceLifetime.Scoped);
        AssertRegistration<IOutboxStore, OutboxStore>(builder.Services, ServiceLifetime.Scoped);
        AssertRegistration<OutboxDispatcher, OutboxDispatcher>(builder.Services, ServiceLifetime.Scoped);
        AssertRegistration<IIntegrationEventPublisher, RebusIntegrationEventPublisher>(
            builder.Services,
            ServiceLifetime.Singleton);
        AssertRegistration<IHostedService, OutboxDispatcherWorker>(builder.Services, ServiceLifetime.Singleton);

        var timeProvider = Assert.Single(
            builder.Services,
            descriptor => descriptor.ServiceType == typeof(TimeProvider));
        Assert.Equal(ServiceLifetime.Singleton, timeProvider.Lifetime);
        Assert.Same(TimeProvider.System, timeProvider.ImplementationInstance);
    }

    [Fact]
    public void Initialize_ValidConfiguration_BindsAndValidatesMessagingOptions()
    {
        var builder = CreateBuilder();
        new InfrastructureModuleInitializer().Initialize(builder);
        using var provider = builder.Services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<SalesMessagingOptions>>().Value;

        Assert.Equal("amqp://guest:guest@localhost:5672", options.ConnectionString);
        Assert.Equal(25, options.OutboxBatchSize);
        Assert.Equal(500, options.PollingIntervalMilliseconds);
        Assert.Equal(45, options.LeaseSeconds);
        Assert.Equal(10, options.InitialRetryDelaySeconds);
        Assert.Equal(120, options.MaximumRetryDelaySeconds);
    }

    [Fact]
    public void Initialize_MaximumRetryBelowInitialRetry_RejectsMessagingOptions()
    {
        var configuration = new Dictionary<string, string?>(ValidMessagingConfiguration)
        {
            [$"{SalesMessagingOptions.SectionName}:InitialRetryDelaySeconds"] = "20",
            [$"{SalesMessagingOptions.SectionName}:MaximumRetryDelaySeconds"] = "10"
        };
        var builder = CreateBuilder(configuration);
        new InfrastructureModuleInitializer().Initialize(builder);
        using var provider = builder.Services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<SalesMessagingOptions>>().Value);

        Assert.Contains(
            "MaximumRetryDelaySeconds must be greater than or equal to InitialRetryDelaySeconds.",
            exception.Failures);
    }

    [Fact]
    public void Initialize_DefaultBuilder_RegistersReadinessHealthChecks()
    {
        var builder = CreateBuilder();
        new InfrastructureModuleInitializer().Initialize(builder);
        using var provider = builder.Services.BuildServiceProvider();

        var registrations = provider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value
            .Registrations;

        var postgresql = Assert.Single(registrations, registration => registration.Name == "postgresql");
        Assert.Equal(HealthStatus.Unhealthy, postgresql.FailureStatus);
        Assert.Contains("readiness", postgresql.Tags);

        var rabbitMq = Assert.Single(registrations, registration => registration.Name == "rabbitmq");
        Assert.Equal(HealthStatus.Unhealthy, rabbitMq.FailureStatus);
        Assert.Contains("readiness", rabbitMq.Tags);
        Assert.Equal(TimeSpan.FromSeconds(5), rabbitMq.Timeout);
        Assert.IsType<RabbitMqHealthCheck>(rabbitMq.Factory(provider));
    }

    [Fact]
    public void Initialize_DefaultBuilder_RegistersControllerAndHealthCheckServices()
    {
        var builder = WebApplication.CreateBuilder();

        new WebApiModuleInitializer().Initialize(builder);

        Assert.Contains(
            builder.Services,
            descriptor => descriptor.ServiceType == typeof(IActionDescriptorCollectionProvider));
        Assert.Contains(
            builder.Services,
            descriptor => descriptor.ServiceType == typeof(HealthCheckService));
    }

    [Fact]
    public void RegisterDependencies_DefaultBuilder_ComposesAllModules()
    {
        var builder = CreateBuilder();

        builder.RegisterDependencies();

        AssertRegistration<IPasswordHasher, BCryptPasswordHasher>(
            builder.Services,
            ServiceLifetime.Singleton);
        AssertRegistration<IUserRepository, UserRepository>(builder.Services, ServiceLifetime.Scoped);
        Assert.Contains(
            builder.Services,
            descriptor => descriptor.ServiceType == typeof(IActionDescriptorCollectionProvider));
    }

    private static WebApplicationBuilder CreateBuilder(
        IEnumerable<KeyValuePair<string, string?>>? configuration = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(configuration ?? ValidMessagingConfiguration);
        return builder;
    }

    private static void AssertRegistration<TService>(
        IServiceCollection services,
        ServiceLifetime expectedLifetime)
    {
        var registration = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(TService));
        Assert.Equal(expectedLifetime, registration.Lifetime);
    }

    private static void AssertRegistration<TService, TImplementation>(
        IServiceCollection services,
        ServiceLifetime expectedLifetime)
    {
        var registration = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(TService)
                && descriptor.ImplementationType == typeof(TImplementation));
        Assert.Equal(expectedLifetime, registration.Lifetime);
    }
}
