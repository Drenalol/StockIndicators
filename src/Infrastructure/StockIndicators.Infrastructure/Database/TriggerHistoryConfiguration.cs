using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Infrastructure.Database;

public class TriggerHistoryConfiguration : IEntityTypeConfiguration<TriggerHistory>
{
  public void Configure(EntityTypeBuilder<TriggerHistory> builder)
  {
    builder.ToTable("TriggerHistory");

    builder.HasKey(t => t.Id);

    builder.Property(t => t.Id)
      .IsRequired()
      .HasColumnType("uuid")
      .ValueGeneratedNever();

    builder.Property(t => t.SubscriptionId)
      .IsRequired()
      .HasColumnType("uuid");

    builder.Property(t => t.TriggerValue)
      .IsRequired()
      .HasColumnType("text");

    builder.Property(t => t.TriggeredAt)
      .IsRequired()
      .HasColumnType("timestamp with time zone");

    builder.Property(t => t.TriggeredCandleAt)
      .HasColumnType("timestamp with time zone");

    // The fired condition as it was configured at alert time (the trigger chart's own copy
    // of it); jsonb like the rule tree it was taken from
    builder.Property(t => t.FiredConditionData)
      .HasColumnType("jsonb");

    builder.HasOne<Subscription>()
      .WithMany()
      .HasForeignKey(t => t.SubscriptionId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasIndex(t => new { t.SubscriptionId, t.TriggeredAt });
  }
}
