using MajdsApp.SharedKernel.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MajdsApp.Modules.Notifications;


/// <summary>An in-app notification addressed to one user (F-Notifications data model).</summary>
public class Notification : MajdsApp.SharedKernel.Data.IEntity<int>
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Type { get; set; } = "General";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>An in-app route the notification opens when clicked (for example /exports); null for a plain message.</summary>
    public string? Link { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>A user's channel choices per notification type; no row means every channel is on (FR-NOTIF-006).</summary>
public class NotificationSubscription
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public NotificationChannel Channels { get; set; } = NotificationChannel.All;
}

/// <summary>Suppressed: deliberately not sent because the channel's delivery mode is Dev (or Test with no test recipient).</summary>
public enum DeliveryStatus { Pending = 0, Sent = 1, Failed = 2, Suppressed = 3 }

/// <summary>Per-recipient, per-channel delivery for channels that go through the background queue
/// (email), with attempt tracking for retry/backoff (FR-NOTIF-004/007).</summary>
public class NotificationDelivery : MajdsApp.SharedKernel.Data.IEntity<int>
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public string Type { get; set; } = "General";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>The whole email body, produced by the template renderer at dispatch time in the recipient's language; null for a delivery made before templates existed.</summary>
    public string? Body { get; set; }
    public DeliveryStatus Status { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.Property(n => n.Type).HasMaxLength(50).HasDefaultValue("General");
        builder.Property(n => n.Title).HasMaxLength(200);
        builder.Property(n => n.Message).HasMaxLength(2000);
        builder.Property(n => n.Link).HasMaxLength(300);
        builder.HasIndex(n => new { n.UserId, n.IsRead });
    }
}

public class NotificationSubscriptionConfiguration : IEntityTypeConfiguration<NotificationSubscription>
{
    public void Configure(EntityTypeBuilder<NotificationSubscription> builder)
    {
        builder.ToTable("NotificationSubscriptions");
        builder.Property(s => s.Type).HasMaxLength(50);
        builder.HasIndex(s => new { s.UserId, s.Type }).IsUnique();
    }
}

public class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("NotificationDeliveries");
        builder.Property(d => d.Type).HasMaxLength(50);
        builder.Property(d => d.Title).HasMaxLength(200);
        builder.Property(d => d.Message).HasMaxLength(2000);
        builder.Property(d => d.Body).HasMaxLength(8000);
        builder.Property(d => d.LastError).HasMaxLength(1000);
        builder.HasIndex(d => new { d.Status, d.NextAttemptAt });
    }
}
