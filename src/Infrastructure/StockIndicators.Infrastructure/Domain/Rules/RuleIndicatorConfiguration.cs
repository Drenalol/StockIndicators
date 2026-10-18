using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Rules;

namespace StockIndicators.Infrastructure.Domain.Rules;

public class RuleIndicatorConfiguration : IEntityTypeConfiguration<RuleIndicator>
{
  public void Configure(EntityTypeBuilder<RuleIndicator> builder)
  {
    builder.ToTable("RuleIndicators");

    builder.HasKey(i => new { i.RuleId, i.Ticker, i.ExchangeName, i.Indicator });

    builder
      .Property(i => i.RuleId)
      .IsRequired()
      .HasColumnType("uuid");

    builder
      .Property(i => i.Ticker)
      .IsRequired()
      .HasColumnType("varchar(20)");

    builder
      .Property(i => i.ExchangeName)
      .IsRequired()
      .HasColumnType("varchar(10)")
      .HasDefaultValue(string.Empty);

    // Indicator names are stored as text (the RuleData jsonb and the wire DTOs use the
    // StringEnumConverter spelling), so filters can match them without enum re-parsing
    builder
      .Property(i => i.Indicator)
      .HasConversion(to => to.ToString(), from => Enum.Parse<Indicator>(from))
      .IsRequired()
      .HasColumnType("varchar(30)");

    builder
      .HasOne<Rule>()
      .WithMany()
      .HasForeignKey(i => i.RuleId)
      .OnDelete(DeleteBehavior.Cascade);

    // The subscription "indicator" filter looks rules up by indicator
    builder.HasIndex(i => i.Indicator);
  }
}
