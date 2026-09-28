using System.Net.Sockets;
using FluentAssertions;
using MailKit.Net.Smtp;
using MajdsApp.Services;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.SharedKernel.Resilience;
using MajdsApp.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Xunit;

namespace MajdsApp.Tests.Unit;

/// <summary>P4 FR-XC-004/006: outbound calls get retry, a timeout and a circuit breaker from one central definition, applied by decorating the sender.</summary>
public class OutboundResilienceTests
{
    private sealed class FakeSender(Func<int, CancellationToken, Task> behaviour) : IEmailMessageSender
    {
        public int Calls;
        public async Task<DeliveryOutcome> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
        {
            await behaviour(++Calls, ct);
            return DeliveryOutcome.Sent;
        }
    }

    private static (ResilientEmailMessageSender Sender, FakeSender Inner) Build(Func<int, CancellationToken, Task> behaviour, Action<OutboundResilienceOptions>? tune = null)
    {
        var options = new OutboundResilienceOptions { MaxRetries = 2, RetryDelayMilliseconds = 1, TimeoutSeconds = 5, BreakMinimumCalls = 4, BreakSamplingSeconds = 60, BreakSeconds = 60 };
        tune?.Invoke(options);
        var inner = new FakeSender(behaviour);
        return (new ResilientEmailMessageSender(inner, new EmailPipeline(Options.Create(options), NullLogger<EmailPipeline>.Instance)), inner);
    }

    private static Task Send(ResilientEmailMessageSender sender) => sender.SendAsync("a@example.com", "s", "b");

    [Fact]
    public async Task A_dropped_connection_is_retried_until_it_works()
    {
        var (sender, inner) = Build((call, _) => call < 3 ? throw new SocketException() : Task.CompletedTask);

        await Send(sender);

        inner.Calls.Should().Be(3);
    }

    [Fact]
    public async Task It_gives_up_after_the_configured_number_of_retries_and_raises_the_last_error()
    {
        var (sender, inner) = Build((_, _) => throw new IOException("reset"));

        var act = () => Send(sender);

        await act.Should().ThrowAsync<IOException>();
        inner.Calls.Should().Be(3); // the first try and two retries
    }

    [Fact]
    public async Task A_wrong_password_is_not_retried_because_retrying_only_delays_the_answer()
    {
        var (sender, inner) = Build((_, _) => throw new MailKit.Security.AuthenticationException("535"));

        var act = () => Send(sender);

        await act.Should().ThrowAsync<MailKit.Security.AuthenticationException>();
        inner.Calls.Should().Be(1);
    }

    [Fact]
    public async Task A_try_again_later_reply_is_retried_but_a_permanent_rejection_is_not()
    {
        var (temporary, tempInner) = Build((call, _) => call < 2 ? throw new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.MailboxBusy, "busy") : Task.CompletedTask);
        await Send(temporary);
        tempInner.Calls.Should().Be(2);

        var (permanent, permInner) = Build((_, _) => throw new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, "no such user"));
        await ((Func<Task>)(() => Send(permanent))).Should().ThrowAsync<SmtpCommandException>();
        permInner.Calls.Should().Be(1);
    }

    [Fact]
    public async Task A_service_that_keeps_failing_opens_the_circuit_and_calls_stop_reaching_it()
    {
        var (sender, inner) = Build((_, _) => throw new IOException("down"), o => o.MaxRetries = 0);

        for (var i = 0; i < 4; i++)
            await ((Func<Task>)(() => Send(sender))).Should().ThrowAsync<IOException>();
        var callsBefore = inner.Calls;

        await ((Func<Task>)(() => Send(sender))).Should().ThrowAsync<BrokenCircuitException>();

        inner.Calls.Should().Be(callsBefore); // the fifth call never touched the service
    }

    [Fact]
    public async Task An_attempt_that_takes_too_long_is_cut_off()
    {
        var (sender, _) = Build(async (_, token) => await Task.Delay(TimeSpan.FromSeconds(30), token), o => { o.TimeoutSeconds = 1; o.MaxRetries = 0; });

        var act = () => Send(sender);

        await act.Should().ThrowAsync<Polly.Timeout.TimeoutRejectedException>();
    }

    [Fact]
    public void The_host_sends_notification_email_through_the_decorator()
    {
        using var factory = new ApiFactory(new Dictionary<string, string> { ["Email:Smtp:Username"] = "u", ["Email:Smtp:Password"] = "p" }, null);

        using var scope = factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IEmailMessageSender>().Should().BeOfType<ResilientEmailMessageSender>();
    }

    [Fact]
    public void Resilience_settings_are_validated_when_the_application_starts()
    {
        using var factory = new ApiFactory(new Dictionary<string, string>
        {
            ["Email:Smtp:Username"] = "u", ["Email:Smtp:Password"] = "p", ["Resilience:TimeoutSeconds"] = "0"
        }, null);

        factory.Invoking(f => f.CreateClient()).Should().Throw<Exception>().Which.ToString().Should().Contain("Resilience:TimeoutSeconds");
    }
}
