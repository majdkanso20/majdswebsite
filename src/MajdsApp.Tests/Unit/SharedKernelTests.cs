using FluentAssertions;
using FluentValidation;
using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Audit;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Paging;
using MajdsApp.SharedKernel.Search;
using MajdsApp.SharedKernel.Settings;
using Xunit;

namespace MajdsApp.Tests.Unit;

public class ResponseDtoTests
{
    [Fact]
    public void Message_defaults_to_the_description_of_the_code()
    {
        var response = ResponseDto.Ok("x");
        response.Message.Should().Be("Operation completed successfully.");
        response.Code.Should().Be(ResponseStatusCode.Success);
    }

    [Fact]
    public void Fail_carries_the_code_message_and_errors()
    {
        var response = ResponseDto.Fail<object>(ResponseStatusCode.ValidationError, "Validation failed.", ["Name is required."]);
        response.Code.Should().Be(ResponseStatusCode.ValidationError);
        response.Message.Should().Be("Validation failed.");
        response.Errors.Should().ContainSingle("Name is required.");
        response.Data.Should().BeNull();
    }

    [Theory]
    [InlineData(ResponseStatusCode.Success, 200)]
    [InlineData(ResponseStatusCode.ValidationError, 400)]
    [InlineData(ResponseStatusCode.Unauthorized, 401)]
    [InlineData(ResponseStatusCode.Forbidden, 403)]
    [InlineData(ResponseStatusCode.NotFound, 404)]
    [InlineData(ResponseStatusCode.Conflict, 409)]
    [InlineData(ResponseStatusCode.TooManyRequests, 429)]
    [InlineData(ResponseStatusCode.Error, 500)]
    public void Codes_are_the_http_status_numbers(ResponseStatusCode code, int status) => ((int)code).Should().Be(status);
}

public class PagedRequestTests
{
    [Fact]
    public void Page_size_is_clamped_to_the_maximum() =>
        new PagedRequest { PageSize = 100_000 }.PageSize.Should().Be(100); // FR-GRID-004

    [Fact]
    public void Page_size_is_never_below_one() => new PagedRequest { PageSize = -5 }.PageSize.Should().Be(1);
}

public class LikePatternTests
{
    [Theory]
    [InlineData("jane", "%jane%")]
    [InlineData("50%", "%50\\%%")]
    [InlineData("a_b", "%a\\_b%")]
    [InlineData("a\\b", "%a\\\\b%")]
    public void User_typed_wildcards_are_matched_literally(string term, string expected) =>
        LikePattern.Contains(term).Should().Be(expected);
}

public class SettingDefinitionTests
{
    public SettingDefinitionTests() => MajdsApp.Tests.Support.ModuleAssemblies.LoadAll();

    [Fact]
    public void Only_declared_settings_allow_a_user_override()
    {
        var byName = SettingDefinitionRegistry.GetAll().ToDictionary(d => d.Name);
        byName["Appearance.Timezone"].AllowUserOverride.Should().BeTrue();
        byName["General.DefaultLanguage"].AllowUserOverride.Should().BeTrue();
        byName["Security.AllowSelfRegistration"].AllowUserOverride.Should().BeFalse();
    }

    [Fact]
    public void Credentials_are_sensitive_and_never_user_overridable()
    {
        var password = SettingDefinitionRegistry.Find("Email.SmtpPassword")!;
        password.IsSensitive.Should().BeTrue();
        password.AllowUserOverride.Should().BeFalse();
    }

    [Fact]
    public void Setting_names_are_unique() =>
        SettingDefinitionRegistry.GetAll().Select(d => d.Name).Should().OnlyHaveUniqueItems();
}

public class AuditOutcomeTests
{
    [Theory]
    [InlineData(typeof(ForbiddenException), "Forbidden")]
    [InlineData(typeof(UnauthorizedAppException), "Unauthorized")]
    [InlineData(typeof(NotFoundException), "NotFound")]
    [InlineData(typeof(ConflictException), "Conflict")]
    [InlineData(typeof(InvalidOperationException), "Error")]
    public void Exceptions_map_to_a_named_outcome(Type exception, string outcome) =>
        AuditRecordFactory.OutcomeFor((Exception)Activator.CreateInstance(exception, "x")!).Should().Be(outcome);

    [Fact]
    public void Validation_failures_map_to_Invalid() =>
        AuditRecordFactory.OutcomeFor(new ValidationException("bad")).Should().Be("Invalid");
}
