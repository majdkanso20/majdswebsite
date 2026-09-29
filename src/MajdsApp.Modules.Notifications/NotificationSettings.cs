using System.ComponentModel.DataAnnotations;
using MajdsApp.SharedKernel.Settings;

namespace MajdsApp.Modules.Notifications;

/// <summary>
/// The delivery environment for notifications (Dev, Test or Prod), edited on the settings page under "Notifications". The rule itself is
/// <c>SettingsDeliveryModePolicy</c> in the Shared Kernel and applies to every channel; a channel added by another module defines its own
/// <c>Notifications.{Channel}.DeliveryMode</c> and <c>Notifications.{Channel}.TestRecipient</c> here in the same way.
/// </summary>
public static class NotificationSettings
{
    private static string? CheckMode(string value, bool allowEmpty) =>
        (allowEmpty && value.Length == 0) || value is "Dev" or "Test" or "Prod" ? null : "The delivery mode must be Dev, Test or Prod.";

    public static class Notifications
    {
        public static readonly SettingDefinition DeliveryMode = new(
            "Notifications.DeliveryMode", "Notifications", "Delivery mode", SettingDataType.String, "Prod",
            description: "Dev: nothing is sent, it is only logged. Test: messages are really sent, but only to the test recipient. Prod: messages go to their real recipients.",
            isVisibleToClient: true, // the mode (not the test recipient) is shown to everyone as a banner when it is not Prod
            validator: value => CheckMode(value, allowEmpty: false));

        public static readonly SettingDefinition EmailDeliveryMode = new(
            "Notifications.Email.DeliveryMode", "Notifications", "Email delivery mode", SettingDataType.String, "",
            description: "Leave empty to use the delivery mode above, or choose Dev, Test or Prod for email alone.",
            isVisibleToClient: true,
            validator: value => CheckMode(value, allowEmpty: true));

        public static readonly SettingDefinition EmailTestRecipient = new(
            "Notifications.Email.TestRecipient", "Notifications", "Email test recipient", SettingDataType.String, "",
            description: "In Test mode every email goes to this address instead of the real recipient. With none set, Test mode sends nothing.",
            validator: value => value.Length == 0 || new EmailAddressAttribute().IsValid(value) ? null : "The test recipient must be a valid email address.");

        public static readonly SettingDefinition SmsDeliveryMode = new(
            "Notifications.Sms.DeliveryMode", "Notifications", "SMS delivery mode", SettingDataType.String, "",
            description: "Leave empty to use the delivery mode above, or choose Dev, Test or Prod for SMS alone.",
            isVisibleToClient: true,
            validator: value => CheckMode(value, allowEmpty: true));

        public static readonly SettingDefinition SmsTestRecipient = new(
            "Notifications.Sms.TestRecipient", "Notifications", "SMS test recipient", SettingDataType.String, "",
            description: "In Test mode every SMS goes to this number instead of the real recipient. With none set, Test mode sends nothing.",
            validator: value => value.Length == 0 || System.Text.RegularExpressions.Regex.IsMatch(value, @"^\+[1-9]\d{1,14}$")
                ? null : "The test recipient must be a phone number in international format, e.g. +15551234567.");

        // A push subscription is one browser on one device, not an address someone can type into a "test recipient" field the
        // way an email or phone number is — there is nothing sensible to redirect Test mode's real sends to. So Push treats Test
        // the same as Dev (logged, never actually sent) and only ever sends for real in Prod; see WebPushOutboundChannel.
        public static readonly SettingDefinition PushDeliveryMode = new(
            "Notifications.Push.DeliveryMode", "Notifications", "Push delivery mode", SettingDataType.String, "",
            description: "Leave empty to use the delivery mode above, or choose Dev or Prod for push alone (Test behaves like Dev: nothing is actually sent).",
            isVisibleToClient: true,
            validator: value => CheckMode(value, allowEmpty: true));

        public static readonly SettingDefinition PushVapidPublicKey = new(
            "Notifications.Push.VapidPublicKey", "Notifications", "Push public key", SettingDataType.String, "",
            description: "Generate a pair below and paste both in — the public key also goes to every browser that subscribes.",
            isVisibleToClient: true);

        public static readonly SettingDefinition PushVapidPrivateKey = new(
            "Notifications.Push.VapidPrivateKey", "Notifications", "Push private key", SettingDataType.String, "",
            description: "Stored encrypted and never shown again. Leave blank to keep the current value.",
            isSensitive: true);

        public static readonly SettingDefinition PushVapidSubject = new(
            "Notifications.Push.VapidSubject", "Notifications", "Push contact", SettingDataType.String, "",
            description: "A mailto: address or URL identifying who is sending — required by the push standard so a browser vendor can reach you about abuse.");
    }
}
