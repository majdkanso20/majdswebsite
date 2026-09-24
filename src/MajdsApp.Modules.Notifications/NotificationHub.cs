using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MajdsApp.Modules.Notifications;

/// <summary>Real-time in-app delivery (FR-NOTIF-005). Users are addressed by their id claim, so the
/// publisher just calls <c>Clients.User(id)</c>; offline users pick notifications up from the list on reconnect.</summary>
[Authorize]
public class NotificationHub : Hub
{
}
