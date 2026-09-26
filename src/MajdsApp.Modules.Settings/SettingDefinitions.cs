using MajdsApp.SharedKernel.Settings;

namespace MajdsApp.Modules.Settings;

/// <summary>
/// The platform's built-in settings, discovered by <c>SettingDefinitionRegistry</c>'s assembly scan.
/// A future feature module adds its own nested static class the same way — no change here.
/// </summary>
public static class SettingDefinitions
{
    public static class General
    {
        public static readonly SettingDefinition ApplicationName = new(
            "General.ApplicationName", "General", "Application name", SettingDataType.String, "Majd's App",
            isVisibleToClient: true, description: "Shown in the shell header and outgoing emails.");

        public static readonly SettingDefinition DefaultLanguage = new(
            "General.DefaultLanguage", "General", "Default language", SettingDataType.String, "en",
            isVisibleToClient: true, description: "UI language for users who haven't picked one (en or ar).",
            allowUserOverride: true);

        public static readonly SettingDefinition SupportEmail = new(
            "General.SupportEmail", "General", "Support email", SettingDataType.String, "",
            isVisibleToClient: true, description: "Shown to users who need help.");
    }

    public static class Email
    {
        // Empty means "use the server configuration" (appsettings / user-secrets), so these only take
        // effect once an administrator fills them in.
        public static readonly SettingDefinition SmtpHost = new(
            "Email.SmtpHost", "Email", "SMTP host", SettingDataType.String, "",
            description: "Outgoing mail server. Leave empty to use the server configuration.");
        public static readonly SettingDefinition SmtpPort = new(
            "Email.SmtpPort", "Email", "SMTP port", SettingDataType.Integer, "0",
            description: "0 uses the server configuration.");
        public static readonly SettingDefinition SmtpUsername = new(
            "Email.SmtpUsername", "Email", "SMTP username", SettingDataType.String, "",
            description: "Leave empty to use the server configuration.");
        public static readonly SettingDefinition SmtpPassword = new(
            "Email.SmtpPassword", "Email", "SMTP password", SettingDataType.String, "",
            description: "Stored encrypted and never shown again. Leave blank to keep the current value.",
            isSensitive: true);
        public static readonly SettingDefinition FromEmail = new(
            "Email.FromEmail", "Email", "From address", SettingDataType.String, "",
            description: "Sender address for outgoing mail. Leave empty to use the server configuration.");
    }

    public static class Appearance
    {
        public static readonly SettingDefinition Timezone = new(
            "Appearance.Timezone", "Appearance", "Time zone", SettingDataType.String, "UTC",
            isVisibleToClient: true, description: "IANA time zone used to display dates and times (for example Asia/Amman).",
            allowUserOverride: true);

        public static readonly SettingDefinition Currency = new(
            "Appearance.Currency", "Appearance", "Currency", SettingDataType.String, "USD",
            isVisibleToClient: true, description: "ISO 4217 currency code used to display amounts of money (for example JOD).",
            allowUserOverride: true);
    }

    public static class Security
    {
        public static readonly SettingDefinition AllowSelfRegistration = new(
            "Security.AllowSelfRegistration", "Security", "Allow users to self-register", SettingDataType.Boolean, "true",
            description: "When off, only an administrator can create new user accounts.");

        public static readonly SettingDefinition DefaultUserSessionTimeoutMinutes = new(
            "Security.DefaultUserSessionTimeoutMinutes", "Security", "Session timeout (minutes)", SettingDataType.Integer, "60",
            description: "How long a sign-in stays valid before the user must sign in again (1 to 1440).");

        public static readonly SettingDefinition MinPasswordLength = new(
            "Security.MinPasswordLength", "Security", "Minimum password length", SettingDataType.Integer, "6",
            description: "The fewest characters a new password may have, when a user is created or a password is set or changed (6 to 128).");
    }
}
