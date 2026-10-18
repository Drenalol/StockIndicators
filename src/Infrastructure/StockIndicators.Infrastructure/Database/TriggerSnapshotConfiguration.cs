using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Infrastructure.Database;

public class TriggerSnapshotConfiguration : IEntityTypeConfiguration<TriggerSnapshot>
{
  public void Configure(EntityTypeBuilder<TriggerSnapshot> builder)
  {
    builder.ToTable("TriggerSnapshots");

    builder.HasKey(t => t.Id);

    builder
      .Property(t => t.Id)
      .IsRequired()
      .HasColumnType("uuid")
      .ValueGeneratedNever();

    builder
      .Property(t => t.SubscriptionId)
      .IsRequired()
      .HasColumnType("uuid");

    builder
      .Property(t => t.Ticker)
      .IsRequired()
      .HasColumnType("varchar(20)");

    // Empty string means "no exchange" (crypto instruments) - the RuleInstruments convention
    builder
      .Property(t => t.ExchangeName)
      .IsRequired()
      .HasColumnType("varchar(10)")
      .HasDefaultValue(string.Empty);

    builder
      .Property(t => t.TimeFrame)
      .IsRequired()
      .HasColumnType("smallint");

    builder
      .Property(t => t.IndicatorNames)
      .IsRequired()
      .HasColumnType("text");

    // Nullable: rows stored before the field existed simply carry no freshness signal
    builder
      .Property(t => t.LastCandleAt)
      .HasColumnType("timestamp with time zone");

    builder
      .Property(t => t.RenderedAt)
      .IsRequired()
      .HasColumnType("timestamp with time zone");

    builder
      .HasOne<Subscription>()
      .WithMany()
      .HasForeignKey(t => t.SubscriptionId)
      .OnDelete(DeleteBehavior.Cascade);

    // One chart per (instrument, timeframe) group of the subscription's conditions
    builder.HasIndex(t => new { t.SubscriptionId, t.Ticker, t.ExchangeName, t.TimeFrame }).IsUnique();
  }
}
