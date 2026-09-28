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
    }
}
