using MajdsApp.SharedKernel.Features;

namespace MajdsApp.Modules.Notifications;

public static class NotificationFeatures
{
    public static class Flags
    {
        public static readonly FeatureDefinition Notifications = new(
            "Notifications", "In-app notifications", defaultEnabled: true,
            description: "The notification bell and sending notifications.");
    }
}
