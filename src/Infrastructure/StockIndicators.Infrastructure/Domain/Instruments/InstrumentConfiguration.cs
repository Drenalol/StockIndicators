using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockIndicators.Domain.Models.Instruments;

namespace StockIndicators.Infrastructure.Domain.Instruments;

/// <summary>
/// Instruments reference table. The tsvector column and the trigram/GIN indexes around the
/// normalized text columns are created by the AddInstrumentsSearch migration in raw SQL
/// (EF does not model generated columns); this configuration maps only the plain columns.
/// </summary>
public class InstrumentConfiguration : IEntityTypeConfiguration<Instrument>
{
  public void Configure(EntityTypeBuilder<Instrument> builder)
  {
    builder.ToTable("Instruments");

    builder.HasKey(i => new { i.Ticker, i.ExchangeName });

    builder
      .Property(i => i.Ticker)
      .IsRequired()
      .HasColumnType("varchar(64)");

    // Empty string means "no exchange" (the RuleInstruments convention), so the key stays NOT NULL
    builder
      .Property(i => i.ExchangeName)
      .IsRequired()
      .HasColumnType("varchar(10)")
      .HasDefaultValue(string.Empty);

    builder
      .Property(i => i.ClassCode)
      .HasColumnType("varchar(20)");

    builder
      .Property(i => i.Currency)
      .HasColumnType("varchar(10)");

    builder
      .Property(i => i.Isin)
      .HasColumnType("varchar(12)");

    builder
      .Property(i => i.Type)
      .HasColumnType("varchar(10)");

    builder
      .Property(i => i.Source)
      .IsRequired()
      .HasColumnType("varchar(20)");

    builder
      .Property(i => i.TickerNorm)
      .IsRequired()
      .HasColumnType("varchar(64)");

    builder
      .Property(i => i.NameNorm)
      .IsRequired()
      .HasColumnType("text")
      .HasDefaultValue(string.Empty);

    builder
      .Property(i => i.NameTranslit)
      .HasColumnType("text");

    builder
      .Property(i => i.Aliases)
      .HasColumnType("text[]")
      .HasDefaultValue(Array.Empty<string>());

    builder
      .Property(i => i.AliasesTop)
      .HasColumnType("text[]")
      .HasDefaultValue(Array.Empty<string>());

    builder
      .Property(i => i.UpdatedAt)
      .IsRequired()
      .HasColumnType("timestamp with time zone")
      .HasDefaultValueSql("CURRENT_TIMESTAMP");

    // Filter columns of the catalog and the subscription filters
    builder.HasIndex(i => i.Type);
    builder.HasIndex(i => i.ExchangeName);
    builder.HasIndex(i => i.TickerNorm);
  }
}
