# MajdsApp.Modules.Notifications

**Notifications (F-Notifications)** — SRS FR-NOTIF-001..009

In-app notifications pushed in real time over SignalR, plus queued email, filtered by each user's per-type, per-channel preferences. Any module notifies users through `IUserNotificationPublisher` (to a user, to everyone, or to a role) without depending on this module.

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `GET /api/notifications/list`
- `GET /api/notifications/subscriptions/get`
- `GET /api/notifications/unread-count`
- `POST /api/notifications/mark-read`
- `POST /api/notifications/send`
- `POST /api/notifications/subscriptions/update`
- `POST /api/notifications/test-channel`

## Permissions

`Notifications.Send` for `send` and `test-channel`; everything else is scoped to the signed-in user.

Declared here: `Notifications.Send`.

## Data

Tables: `NotificationDeliveries`, `NotificationSubscriptions`, `Notifications`. Migrations live in `MajdsApp.Core`.

## Recurring jobs

- *Notification cleanup*

## Notes

- Real-time: hub at `/hubs/notifications`. Browsers cannot set headers on a WebSocket, so the token is accepted from the `access_token` query string on `/hubs` paths only.
- Types are `General`, `Security`, `Account`, `Administration` (see `NotificationTypes`); channels are in-app and email. With no saved preference every channel is on.
- A notification may carry an in-app `link` (for example `/exports`); the bell opens it when clicked. Publishers pass it through the optional `link` argument of `IUserNotificationPublisher.PublishAsync`. Email copies carry the text only.
- Email goes through a queue with retry and exponential backoff (three attempts, then marked failed). `POST /api/notifications/test-channel` sends one email to the caller immediately and returns the real error.
- Not implemented: SMS and push channels, a shared channel interface, localized templates, a Redis backplane.
- The module is behind the `Notifications` feature flag.

## Configuration keys

- Feature flag `Notifications`
- Email uses the Email settings (host, port, user, encrypted password, from address) or the `Email:Smtp:*` configuration

## Tests

Covered by the integration tests in `src/MajdsApp.Tests` (run `dotnet test src/MajdsApp.Tests`).
