using Ambev.DeveloperEvaluation.Common.Security;
using Ambev.DeveloperEvaluation.IoC.ModuleInitializers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.IoC;

public sealed class ApplicationModuleInitializerTests
{
    [Fact]
    public void Initialize_DefaultBuilder_RegistersPasswordHasherAsSingleton()
    {
        var builder = WebApplication.CreateBuilder();

        new ApplicationModuleInitializer().Initialize(builder);

        var registration = Assert.Single(
            builder.Services,
            descriptor => descriptor.ServiceType == typeof(IPasswordHasher));
        Assert.Equal(ServiceLifetime.Singleton, registration.Lifetime);
        Assert.Equal(typeof(BCryptPasswordHasher), registration.ImplementationType);

        using var provider = builder.Services.BuildServiceProvider();
        Assert.Same(
            provider.GetRequiredService<IPasswordHasher>(),
            provider.GetRequiredService<IPasswordHasher>());
    }
}
