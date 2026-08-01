using Ambev.DeveloperEvaluation.Common.Security;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Common;

public sealed class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_PlainTextPassword_ReturnsVerifiableBCryptHash()
    {
        const string password = "Valid-password-123!";

        var hash = _hasher.HashPassword(password);

        hash.Should().NotBe(password);
        hash.Should().StartWith("$2");
        _hasher.VerifyPassword(password, hash).Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_IncorrectPassword_ReturnsFalse()
    {
        var hash = _hasher.HashPassword("correct-password");

        var result = _hasher.VerifyPassword("incorrect-password", hash);

        result.Should().BeFalse();
    }
}
