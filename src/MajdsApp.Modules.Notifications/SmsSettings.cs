using MajdsApp.SharedKernel.Settings;

namespace MajdsApp.Modules.Notifications;

/// <summary>
/// Which SMS provider <see cref="SmsOutboundChannel"/> sends through, and that provider's own credentials — the same
/// swap-the-implementation idea as <c>MajdsApp.Modules.Files</c>' storage providers (one interface, several classes,
/// pick one by name), but chosen live from the settings page instead of at startup, since a provider's credentials are
/// exactly the kind of thing an administrator rotates without a redeploy. Twilio, Vonage and Infobip are built in as
/// proof; a module or plugin can add a fourth by implementing <see cref="ISmsGateway"/> and registering its own settings the same way.
/// </summary>
public static class SmsSettings
{
    public static class Sms
    {
        public static readonly SettingDefinition Provider = new(
            "Sms.Provider", "Sms", "SMS provider", SettingDataType.String, "",
            description: "Leave empty to leave SMS unavailable. Twilio, Vonage and Infobip are built in.",
            validator: value => value.Length == 0 || value is "Twilio" or "Vonage" or "Infobip" ? null : "The SMS provider must be Twilio, Vonage or Infobip.");

        public static readonly SettingDefinition TwilioAccountSid = new(
            "Sms.Twilio.AccountSid", "Sms", "Twilio account SID", SettingDataType.String, "");

        public static readonly SettingDefinition TwilioAuthToken = new(
            "Sms.Twilio.AuthToken", "Sms", "Twilio auth token", SettingDataType.String, "",
            description: "Stored encrypted and never shown again. Leave blank to keep the current value.",
            isSensitive: true);

        public static readonly SettingDefinition TwilioFromNumber = new(
            "Sms.Twilio.FromNumber", "Sms", "Twilio sender number", SettingDataType.String, "",
            description: "The Twilio phone number messages are sent from, e.g. +15551234567.");

        public static readonly SettingDefinition VonageApiKey = new(
            "Sms.Vonage.ApiKey", "Sms", "Vonage API key", SettingDataType.String, "");

        public static readonly SettingDefinition VonageApiSecret = new(
            "Sms.Vonage.ApiSecret", "Sms", "Vonage API secret", SettingDataType.String, "",
            description: "Stored encrypted and never shown again. Leave blank to keep the current value.",
            isSensitive: true);

        public static readonly SettingDefinition VonageFromNumber = new(
            "Sms.Vonage.FromNumber", "Sms", "Vonage sender ID", SettingDataType.String, "",
            description: "The Vonage sender ID or number messages are sent from.");

        public static readonly SettingDefinition InfobipBaseUrl = new(
            "Sms.Infobip.BaseUrl", "Sms", "Infobip base URL", SettingDataType.String, "",
            description: "The personalized API base URL from the Infobip dashboard, e.g. https://xxxxxx.api.infobip.com.");

        public static readonly SettingDefinition InfobipApiKey = new(
            "Sms.Infobip.ApiKey", "Sms", "Infobip API key", SettingDataType.String, "",
            description: "Stored encrypted and never shown again. Leave blank to keep the current value.",
            isSensitive: true);

        public static readonly SettingDefinition InfobipFromNumber = new(
            "Sms.Infobip.FromNumber", "Sms", "Infobip sender ID", SettingDataType.String, "",
            description: "The Infobip sender ID or number messages are sent from.");
    }
}
