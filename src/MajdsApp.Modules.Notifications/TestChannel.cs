using FluentValidation;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Localization;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.SharedKernel.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Notifications;

/// <summary>Sends one email to the caller right now, bypassing the queue, so an administrator can check
/// the mail settings and see the actual error (FR-NOTIF-008).</summary>
[RequiresPermission(Permissions.Notifications.Send)]
public record TestEmailChannelCommand : IRequest, IAuditableCommand;

public class TestEmailChannelCommandHandler(
    ApplicationDbContext db, ICurrentUser currentUser, IMessageCatalog catalog, IEmailMessageSender? sender = null)
    : IRequestHandler<TestEmailChannelCommand>
{
    public async Task Handle(TestEmailChannelCommand request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Authentication is required.");
        var email = await db.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync(ct);

        if (sender is null || string.IsNullOrEmpty(email))
            throw new ValidationException("Email is not configured on this server, or your account has no email address.");

        try
        {
            var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
            await sender.SendAsync(email, catalog.Translate("Test email", culture),
                $"<p>{catalog.Translate("This is a test email from the notification settings.", culture)}</p>", ct);
        }
        catch (Exception ex)
        {
            var reason = ex.Message.Split('\n')[0];
            throw new ValidationException($"Sending failed: {reason}");
        }
    }
}
