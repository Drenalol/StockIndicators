using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockIndicators.Domain.Models.PushSubscriptions;

namespace StockIndicators.Infrastructure.Domain.PushSubscriptions;

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
  public void Configure(EntityTypeBuilder<PushSubscription> builder)
  {
    builder.HasKey(p => p.Id);

    builder
      .Property(p => p.Id)
      .IsRequired()
      .HasColumnType("uuid");

    builder
      .Property(p => p.UserId)
      .IsRequired()
      .HasColumnType("uuid");

    builder
      .Property(p => p.Platform)
      .IsRequired()
      .HasColumnType("varchar(10)")
      .HasConversion(to => to.ToString(), from => Enum.Parse<PushPlatform>(from));

    builder
      .Property(p => p.Endpoint)
      .IsRequired()
      .HasColumnType("text");

    builder
      .Property(p => p.PublicKey)
      .IsRequired(false)
      .HasColumnType("text");

    builder
      .Property(p => p.AuthKey)
      .IsRequired(false)
      .HasColumnType("text");

    builder
      .Property(p => p.DeviceLabel)
      .IsRequired(false)
      .HasColumnType("varchar(100)");

    builder
      .Property(p => p.Created)
      .IsRequired()
      .HasColumnType("timestamp with time zone")
      .HasDefaultValueSql("CURRENT_TIMESTAMP");

    builder
      .Property(p => p.Updated)
      .IsRequired(false)
      .HasColumnType("timestamp with time zone");

    builder
      .HasOne(p => p.User)
      .WithMany()
      .HasForeignKey(p => p.UserId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasIndex(p => new { p.UserId, p.Endpoint }).IsUnique();
  }
}
