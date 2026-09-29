using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MajdsApp.Modules.Notifications;

/// <summary>
/// One browser's subscription to push notifications (F-Notifications FR-NOTIF-002/009: push as a channel). A user can have several —
/// one per device/browser they enabled it on — so <see cref="WebPushOutboundChannel"/> sends to every one of them, and drops a
/// subscription the browser itself reports as gone (410) rather than retrying it forever.
/// </summary>
public class PushDeviceSubscription : MajdsApp.SharedKernel.Data.IEntity<int>
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    /// <summary>The browser's push service URL for this subscription — unique per browser/device, so it also identifies which one this is.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>The two keys the Push API gives the page when it subscribes, needed to encrypt a message this subscription can decrypt.</summary>
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

public class PushDeviceSubscriptionConfiguration : IEntityTypeConfiguration<PushDeviceSubscription>
{
    public void Configure(EntityTypeBuilder<PushDeviceSubscription> builder)
    {
        builder.ToTable("PushSubscriptions");
        builder.Property(s => s.UserId).HasMaxLength(450);
        builder.Property(s => s.Endpoint).HasMaxLength(1000);
        builder.Property(s => s.P256dh).HasMaxLength(200);
        builder.Property(s => s.Auth).HasMaxLength(200);
        builder.HasIndex(s => new { s.UserId, s.Endpoint }).IsUnique();
    }
}
