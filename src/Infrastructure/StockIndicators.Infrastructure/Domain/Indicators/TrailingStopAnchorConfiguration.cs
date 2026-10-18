using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockIndicators.Domain.Models.Indicators;

namespace StockIndicators.Infrastructure.Domain.Indicators;

/// <summary>
/// The trailing-stop anchor lives in the dedicated <c>indicators</c> schema: it is indicator
/// runtime state, not user data - keyed by the rule condition id (no FK: anchors are
/// best-effort and orphan cleanup follows the rule write paths, not the database).
/// </summary>
public class TrailingStopAnchorConfiguration : IEntityTypeConfiguration<TrailingStopAnchor>
{
  public void Configure(EntityTypeBuilder<TrailingStopAnchor> builder)
  {
    builder.ToTable("TrailingStopAnchors", "indicators");

    builder
      .HasKey(a => a.ConditionId);

    builder
      .Property(a => a.ConditionId)
      .HasColumnType("uuid");

    builder
      .Property(a => a.Anchor)
      .IsRequired()
      .HasColumnType("double precision");

    builder
      .Property(a => a.Updated)
      .IsRequired()
      .HasColumnType("timestamp with time zone");
  }
}
