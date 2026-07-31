using Ambev.DeveloperEvaluation.Common.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Common;

public sealed class AuthenticationExtensionTests
{
    private const string SecretKey = "unit-test-secret-key-with-32-bytes-minimum";

    [Fact]
    public void AddJwtAuthentication_ValidConfiguration_RegistersScopedTokenGeneratorAndBearerDefaults()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(SecretKey);

        var returnedServices = services.AddJwtAuthentication(configuration);

        returnedServices.Should().BeSameAs(services);
        var registration = services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IJwtTokenGenerator)).Which;
        registration.ImplementationType.Should().Be<JwtTokenGenerator>();
        registration.Lifetime.Should().Be(ServiceLifetime.Scoped);

        using var provider = services.BuildServiceProvider();
        var authenticationOptions = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        authenticationOptions.DefaultAuthenticateScheme.Should().Be(JwtBearerDefaults.AuthenticationScheme);
        authenticationOptions.DefaultChallengeScheme.Should().Be(JwtBearerDefaults.AuthenticationScheme);
    }

    [Fact]
    public void AddJwtAuthentication_ValidConfiguration_ConfiguresTokenValidationParameters()
    {
        var services = new ServiceCollection();
        services.AddJwtAuthentication(CreateConfiguration(SecretKey));

        using var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        options.RequireHttpsMetadata.Should().BeFalse();
        options.SaveToken.Should().BeTrue();
        options.TokenValidationParameters.ValidateIssuerSigningKey.Should().BeTrue();
        options.TokenValidationParameters.ValidateIssuer.Should().BeFalse();
        options.TokenValidationParameters.ValidateAudience.Should().BeFalse();
        options.TokenValidationParameters.ClockSkew.Should().Be(TimeSpan.Zero);
        var signingKey = options.TokenValidationParameters.IssuerSigningKey
            .Should().BeOfType<SymmetricSecurityKey>().Subject;
        signingKey.Key.Should().Equal(System.Text.Encoding.ASCII.GetBytes(SecretKey));
    }

    [Fact]
    public void AddJwtAuthentication_MissingSecretKey_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(secretKey: null);

        var act = () => services.AddJwtAuthentication(configuration);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("secretKey");
    }

    private static IConfiguration CreateConfiguration(string? secretKey)
    {
        var values = secretKey is null
            ? []
            : new Dictionary<string, string?> { ["Jwt:SecretKey"] = secretKey };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
