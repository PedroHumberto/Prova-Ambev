using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ambev.DeveloperEvaluation.Common.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Common;

public sealed class JwtTokenGeneratorTests
{
    private const string SecretKey = "unit-test-secret-key-with-32-bytes-minimum";

    [Fact]
    public void GenerateToken_ValidUser_ReturnsSignedTokenWithIdentityClaimsAndEightHourExpiration()
    {
        var generator = new JwtTokenGenerator(CreateConfiguration(SecretKey));
        var user = new TestUser("user-123", "test.user", "Administrator");
        var generationStarted = DateTime.UtcNow;

        var token = generator.GenerateToken(user);

        var generationCompleted = DateTime.UtcNow;
        var handler = new JwtSecurityTokenHandler();
        handler.ValidateToken(token, CreateValidationParameters(), out _);

        var jwt = handler.ReadJwtToken(token);
        jwt.Claims.Should().ContainSingle(claim =>
            claim.Type == JwtRegisteredClaimNames.NameId && claim.Value == user.Id);
        jwt.Claims.Should().ContainSingle(claim =>
            (claim.Type == ClaimTypes.Role || claim.Type == "role") && claim.Value == user.Role);
        jwt.Header.Alg.Should().Be(SecurityAlgorithms.HmacSha256);
        jwt.ValidTo.Should().BeOnOrAfter(generationStarted.AddHours(8).AddSeconds(-1));
        jwt.ValidTo.Should().BeOnOrBefore(generationCompleted.AddHours(8).AddSeconds(1));
    }

    [Fact]
    public void GenerateToken_MissingSecretKey_ThrowsArgumentException()
    {
        var generator = new JwtTokenGenerator(CreateConfiguration(secretKey: null));
        var user = new TestUser("user-123", "test.user", "Administrator");

        var act = () => generator.GenerateToken(user);

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

    private static TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(SecretKey)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    private sealed record TestUser(string Id, string Username, string Role) : IUser;
}
