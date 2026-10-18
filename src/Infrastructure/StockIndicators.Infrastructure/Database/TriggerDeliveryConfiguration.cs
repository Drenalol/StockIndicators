using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockIndicators.Domain.Models.Notifications;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Infrastructure.Database;

public class TriggerDeliveryConfiguration : IEntityTypeConfiguration<TriggerDelivery>
{
  public void Configure(EntityTypeBuilder<TriggerDelivery> builder)
  {
    builder.ToTable("TriggerDeliveries");

    builder.HasKey(d => d.Id);

    builder.Property(d => d.Id)
      .IsRequired()
      .HasColumnType("uuid")
      .ValueGeneratedNever();

    builder.Property(d => d.TriggerHistoryId)
      .IsRequired()
      .HasColumnType("uuid");

    builder.Property(d => d.Channel)
      .IsRequired()
      .HasColumnType("varchar(20)")
      .HasConversion(to => to.ToString(), from => Enum.Parse<NotificationChannel>(from));

    builder.Property(d => d.Status)
      .IsRequired()
      .HasColumnType("varchar(10)")
      .HasConversion(to => to.ToString(), from => Enum.Parse<DeliveryStatus>(from));

    builder.Property(d => d.Error)
      .IsRequired(false)
      .HasColumnType("text");

    builder.Property(d => d.SentAt)
      .IsRequired()
      .HasColumnType("timestamp with time zone");

    builder.HasOne<TriggerHistory>()
      .WithMany(h => h.Deliveries)
      .HasForeignKey(d => d.TriggerHistoryId)
      .OnDelete(DeleteBehavior.Cascade);

    // One delivery row per trigger and channel per flush attempt
    builder.HasIndex(d => new { d.TriggerHistoryId, d.Channel });
  }
}
