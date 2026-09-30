# MajdsApp.Modules.Notifications

**Notifications (F-Notifications)** — SRS FR-NOTIF-001..009

In-app notifications pushed in real time over SignalR, plus queued email, filtered by each user's per-type, per-channel preferences. Any module notifies users through `IUserNotificationPublisher` (to a user, to everyone, or to a role) without depending on this module — or, for the single call the SRS names (FR-NOTIF-001), `INotificationDispatcher.SendAsync(new NotificationMessage(title, message, ...), recipientUserIds)`. Both interfaces are the same `NotificationPublisher` instance per scope, so either can be injected.

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `GET /api/notifications/list`
- `GET /api/notifications/subscriptions/get`
- `GET /api/notifications/unread-count`
- `POST /api/notifications/mark-read`
- `POST /api/notifications/send`
- `POST /api/notifications/subscriptions/update`
- `POST /api/notifications/test-channel`
- `GET /api/notifications/push/vapid-public-key` — the public half of the saved VAPID key pair, or null when push is not set up
- `POST /api/notifications/push/subscribe` — registers this browser's push subscription for the signed-in user
- `POST /api/notifications/push/unsubscribe`
- `GET /api/notifications/push/generate-vapid-keys` — a fresh key pair for the Push settings page's "Generate" button (`Notifications.Send`)

## Permissions

`Notifications.Send` for `send` and `test-channel`; everything else is scoped to the signed-in user.

Declared here: `Notifications.Send`.

## Data

Tables: `NotificationDeliveries`, `NotificationSubscriptions`, `Notifications`, `PushSubscriptions` (one row per browser/device a user enabled push on). Migrations live in `MajdsApp.Core`.

## Templates (FR-NOTIF-003)

The dispatcher never builds channel text itself. For each recipient it asks `INotificationTemplateRenderer` (in `MajdsApp.SharedKernel.Notifications`) for the content of each channel it will use, giving the type, the source-language title and message, and the recipient's language. The default renderer translates the text; for email it also produces an HTML body whose direction follows the language (right to left for Arabic) and encodes every piece of text. To change the wording or layout of one type on one channel, register an `INotificationTemplate` (any module) with that `Type` and `Channel`; it replaces the default for exactly that pair. `SecurityEmailTemplate` adds a translated "If this was not you, contact your administrator" line to Security emails. The rendered email body is stored on the queued delivery (`NotificationDeliveries.Body`), so a retry sends the same message.

## Adding a channel

A channel is one class in any module or plugin that implements `IOutboundChannel` (in `MajdsApp.SharedKernel.Notifications`) and is registered with `services.AddScoped<IOutboundChannel, MyChannel>()`. `SmsOutboundChannel.cs` is a real, working example:

```csharp
public class SmsOutboundChannel(ApplicationDbContext db, ISettingsProvider settings, IEnumerable<ISmsGateway> gateways) : IOutboundChannel
{
    public string Name => "Sms";                 // stored in deliveries and preferences; names the settings below
    public string DisplayName => "SMS";          // the column heading in a user's notification preferences
    public bool EnabledByDefault => false;       // it costs money and needs a number, so people opt in
    public Task<string?> ResolveAddressAsync(string userId, CancellationToken ct) => /* the user's phone number, or null */;
    public Task<DeliveryOutcome> SendAsync(string address, OutboundMessage message, CancellationToken ct) => /* pick a gateway by the Sms.Provider setting; throw to retry later */;
}
```

That is all. The dispatcher queues a message for every user who has the channel on for that notification type, the worker delivers it with retry and backoff (three attempts, then Failed), the delivery mode is applied before `SendAsync` is called (so Dev logs and Test redirects with no code in the channel), and the channel appears as a column in each user's notification preferences. The channel defines its own `Notifications.Sms.DeliveryMode` and `Notifications.Sms.TestRecipient` settings, as `Notifications.Email.*` are defined in `NotificationSettings.cs`. A channel that must apply the mode itself (email does, so password-reset mail follows it too) sets `AppliesDeliveryModeItself`. Message text for a module channel is the plain, translated title and message.

Storage: `NotificationDeliveries.ChannelName` names the channel of each queued message (older rows, with none, are email); a user's choice on a module channel is one row in `NotificationChannelChoices`, kept only when it differs from the channel's default.

### SMS: swapping the provider without touching the channel

`SmsOutboundChannel` never talks to a provider itself — it resolves a user's saved phone number, then hands the message to whichever `ISmsGateway` the `Sms.Provider` setting names (`SmsGateways.cs`). Three are built in, each with a genuinely different request shape: Twilio (HTTP Basic auth, a form-encoded body), Vonage (the key and secret inside a JSON body, and a 200 response even for many failures — the real result is a per-message status code inside it), and Infobip (an `App`-scheme authorization header carrying the API key, a JSON body of `destinations`, and its own per-account base URL rather than one fixed host). Adding a fourth provider is the same shape as adding a channel: implement `ISmsGateway`, register it (`services.AddScoped<ISmsGateway, MyGateway>()`), and it appears as a choice in the `Sms.Provider` setting's validator. Credentials live under their own `Sms` settings group (`Sms.Twilio.*`, `Sms.Vonage.*`, `Sms.Infobip.*`), separate from the `Notifications.Sms.DeliveryMode`/`TestRecipient` pair every channel gets — the same split `Email.*` (server/credentials) and `Notifications.Email.*` (delivery mode) already use.

### Push: no provider needed

`WebPushOutboundChannel` (`WebPushOutboundChannel.cs`) reaches every subscribed browser directly over the W3C Push API, signed with a VAPID key pair an administrator generates on the settings page (`Notifications.Push.VapidPublicKey/VapidPrivateKey/VapidSubject`) — no third-party push service account is involved on either side. A browser subscribes through Angular's own service worker (`SwPush`) from the Account page; each subscription is one row in `PushSubscriptions` (one per device), so a user with several devices gets a delivery to each, and a `410`/`404` from a subscription's push service (it expired or was revoked) deletes that row instead of retrying it forever. Push has no meaningful "test recipient" — a subscription is one browser on one device, not an address to redirect to — so it deliberately behaves like Dev in Test mode (nothing is actually sent) with no special-case code, the same generic delivery-mode policy every channel gets.

## Delivery mode: Dev, Test, Prod

Every channel obeys a delivery mode, set under **Settings, Notifications** (administrators only, and recorded in the audit log):

| Mode | A real message is sent? | To whom | What is recorded |
|---|---|---|---|
| **Dev** | No | nobody | a log line: `[Dev] Email NOT sent (...). It was for x@y: 'subject'`; a queued notification is marked *Suppressed* |
| **Test** | Yes | only the channel's **test recipient** (subject prefixed `[Test for x@y]`) | a log line naming the real recipient and the test recipient |
| **Prod** | Yes | the real recipient | the normal delivery record |

Settings: `Notifications.DeliveryMode` (the general mode, default **Prod** so an existing installation keeps sending), `Notifications.Email.DeliveryMode` (email's own mode; empty follows the general one) and `Notifications.Email.TestRecipient`. A channel added by another module defines `Notifications.{Channel}.DeliveryMode` and `Notifications.{Channel}.TestRecipient` the same way and asks `IDeliveryModePolicy` before it sends.

Safety: in Test mode with no test recipient, or with a mode that is not Dev, Test or Prod, **nothing is sent** (a missing or wrong setting can only make the system quieter, never message a real person). The rule is applied inside the mail sender, the one place every email leaves the system, so it covers notifications, password-reset and confirmation emails, and the "Send test email" button (which reports "Nothing was sent" in Dev).

When a channel is not in Prod, the website shows a banner under the header to every signed-in user ("Dev mode: Email notifications are not sent, they are only logged." or "Test mode: ... go only to the test recipient."). It reads the two mode settings, which are visible to clients (the test recipient is not), and updates as soon as the settings page saves.

## Recurring jobs

- *Notification cleanup*

## Notes

- Real-time: hub at `/hubs/notifications`. Browsers cannot set headers on a WebSocket, so the token is accepted from the `access_token` query string on `/hubs` paths only.
- Behind a load balancer with more than one node, set `SignalR:Redis:ConnectionString` (NFR-SCALE-2) so a push reaches a user connected to a different node; with none, each node only knows about its own connections. Uses the same `Cache:KeyPrefix` so several deployments can share one Redis without crossing wires.
- Types are `General`, `Security`, `Account`, `Administration` (see `NotificationTypes`); channels are in-app, email, push and SMS. With no saved preference, in-app and email are on and push and SMS (which both need something a user opts into first — a subscription or a phone number) are off.
- A notification may carry an in-app `link` (for example `/exports`); the bell opens it when clicked. Publishers pass it through the optional `link` argument of `IUserNotificationPublisher.PublishAsync`. Email copies carry the text only.
- Every notification also carries a `severity` (`Info`, `Success`, `Warning`, `Error`; FR-NOTIF-004) and an optional `payload` — small, arbitrary JSON a client can read back, for example the id of the export it is about. Both default to nothing extra (`Info`, no payload) when a caller does not pass them. The bell menu gives `Warning` and `Error` a colored edge; `Info` and `Success` stay plain.
- Email goes through a queue with retry and exponential backoff (three attempts, then marked failed). `POST /api/notifications/test-channel` sends one email to the caller immediately and returns the real error.
- The module is behind the `Notifications` feature flag.

## Configuration keys

- Feature flag `Notifications`
- Email uses the Email settings (host, port, user, encrypted password, from address) or the `Email:Smtp:*` configuration
- Push uses the Notifications settings (`Notifications.Push.VapidPublicKey/VapidPrivateKey/VapidSubject`) — self-generated, no external configuration
- SMS uses the Sms settings (`Sms.Provider`, plus `Sms.Twilio.*`, `Sms.Vonage.*` or `Sms.Infobip.*` depending on which is chosen)

## Tests

Covered by the integration tests in `src/MajdsApp.Tests` (run `dotnet test src/MajdsApp.Tests`).
