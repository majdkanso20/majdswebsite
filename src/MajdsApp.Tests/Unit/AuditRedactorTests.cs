using FluentAssertions;
using MajdsApp.SharedKernel.Audit;
using Xunit;

namespace MajdsApp.Tests.Unit;

/// <summary>FR-AUDIT-004: secrets never reach the audit trail.</summary>
public class AuditRedactorTests
{
    [Theory]
    [InlineData("Password")]
    [InlineData("NewPassword")]
    [InlineData("PasswordHash")]
    [InlineData("SecurityStamp")]
    [InlineData("AccessToken")]
    [InlineData("ClientSecret")]
    [InlineData("RecoveryCodes")]
    [InlineData("Code")]
    public void Credential_like_names_are_sensitive(string name) =>
        AuditRedactor.IsSensitive(name).Should().BeTrue();

    [Theory]
    [InlineData("Email")]
    [InlineData("DisplayName")]
    [InlineData("IsActive")]
    [InlineData("Value")]
    public void Ordinary_names_are_not_sensitive(string name) =>
        AuditRedactor.IsSensitive(name).Should().BeFalse();

    [Fact]
    public void Sensitive_values_are_replaced_by_a_placeholder() =>
        AuditRedactor.Redact("PasswordHash", "AQAAAAEAACcQ...").Should().Be(AuditRedactor.Placeholder);

    [Fact]
    public void Encrypted_setting_values_are_redacted_even_under_an_innocent_name() =>
        AuditRedactor.Redact("Value", "enc:CfDJ8abc").Should().Be(AuditRedactor.Placeholder);

    [Fact]
    public void Null_stays_null() => AuditRedactor.Redact("Password", null).Should().BeNull();

    [Fact]
    public void Long_values_are_truncated()
    {
        var redacted = AuditRedactor.Redact("Notes", new string('x', 2000));
        redacted!.Length.Should().BeLessThan(600);
    }

    private record Nested(string Email, string Password);
    private record Command(string Name, string Password, Nested Inner, List<Nested> Items, Stream Content);

    [Fact]
    public void Parameters_are_redacted_at_every_depth_and_binary_content_is_never_written()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        var command = new Command("Ada", "hunter2", new Nested("a@b.co", "inner-secret"),
            [new Nested("c@d.co", "list-secret")], stream);

        var json = AuditRedactor.SerializeParameters(command)!;

        json.Should().Contain("Ada").And.Contain("a@b.co");
        json.Should().NotContain("hunter2").And.NotContain("inner-secret").And.NotContain("list-secret");
        json.Should().Contain("[binary]");
    }
}
