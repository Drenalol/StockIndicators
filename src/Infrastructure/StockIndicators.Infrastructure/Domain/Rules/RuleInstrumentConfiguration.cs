using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockIndicators.Domain.Models.Rules;

namespace StockIndicators.Infrastructure.Domain.Rules;

public class RuleInstrumentConfiguration : IEntityTypeConfiguration<RuleInstrument>
{
  public void Configure(EntityTypeBuilder<RuleInstrument> builder)
  {
    builder.ToTable("RuleInstruments");

    builder.HasKey(i => new { i.RuleId, i.Ticker, i.ExchangeName });

    builder
      .Property(i => i.RuleId)
      .IsRequired()
      .HasColumnType("uuid");

    builder
      .Property(i => i.Ticker)
      .IsRequired()
      .HasColumnType("varchar(20)");

    // Empty string means "no exchange" (crypto instruments), so the composite key stays NOT NULL
    builder
      .Property(i => i.ExchangeName)
      .IsRequired()
      .HasColumnType("varchar(10)")
      .HasDefaultValue(string.Empty);

    // The projection lives and dies with the rule
    builder
      .HasOne<Rule>()
      .WithMany()
      .HasForeignKey(i => i.RuleId)
      .OnDelete(DeleteBehavior.Cascade);

    // The hot lookup: instruments of an arrived candle batch -> rule ids -> subscriptions
    builder.HasIndex(i => new { i.Ticker, i.ExchangeName });
  }
}
