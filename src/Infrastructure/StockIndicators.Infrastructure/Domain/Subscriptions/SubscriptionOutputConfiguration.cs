using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockIndicators.Domain.Models.Notifications;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Infrastructure.Domain.Subscriptions;

public class SubscriptionOutputConfiguration : IEntityTypeConfiguration<SubscriptionOutput>
{
  public void Configure(EntityTypeBuilder<SubscriptionOutput> builder)
  {
    builder.HasKey(o => o.Id);

    builder
      .Property(o => o.Id)
      .IsRequired()
      .HasColumnType("uuid");

    builder
      .Property(o => o.SubscriptionId)
      .IsRequired()
      .HasColumnType("uuid");

    builder
      .Property(o => o.Channel)
      .IsRequired()
      .HasColumnType("varchar(20)")
      .HasConversion(to => to.ToString(), from => Enum.Parse<NotificationChannel>(from));

    builder
      .Property(o => o.NotificationTemplateId)
      .IsRequired(false)
      .HasColumnType("uuid");

    builder
      .Property(o => o.PushSubscriptionId)
      .IsRequired(false)
      .HasColumnType("uuid");

    builder
      .Property(o => o.IsEnabled)
      .IsRequired()
      .HasColumnType("boolean")
      .HasDefaultValue(true);

    // Telegram delivery target (the linked chat id). Resolved per user in Phase 4 of the auth work
    builder
      .Property(o => o.ChatId)
      .IsRequired(false)
      .HasColumnType("bigint");

    // Destination URL (Webhook/Discord) or E.164 phone (WhatsApp)
    builder
      .Property(o => o.Target)
      .IsRequired(false)
      .HasColumnType("varchar(2048)");

    builder
      .HasOne(o => o.NotificationTemplate)
      .WithMany()
      .HasForeignKey(o => o.NotificationTemplateId)
      .OnDelete(DeleteBehavior.SetNull);

    builder
      .HasOne(o => o.PushSubscription)
      .WithMany()
      .HasForeignKey(o => o.PushSubscriptionId)
      .OnDelete(DeleteBehavior.SetNull);

    // A subscription can have at most one Telegram output (target is a linked chat of the owner).
    // The same "one per channel" rule holds for every other channel, but it is enforced in
    // application validation: EF merges multiple fluent indexes declared on the same column.
    builder
      .HasIndex(o => o.SubscriptionId)
      .IsUnique()
      .HasFilter("\"Channel\" = 'Telegram'");
  }
}
