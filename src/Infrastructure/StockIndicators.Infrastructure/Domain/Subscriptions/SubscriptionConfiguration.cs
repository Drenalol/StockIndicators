using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;

namespace StockIndicators.Infrastructure.Domain.Subscriptions;

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
  public void Configure(EntityTypeBuilder<Subscription> builder)
  {
    builder.HasKey(s => new { s.Id });

    builder
      .Property(s => s.Id)
      .IsRequired()
      .HasColumnType("uuid");

    builder
      .Property(s => s.Status)
      .IsRequired()
      .HasMaxLength(20)
      .HasConversion(to => to.ToString(), from => Enum.Parse<SubscriptionStatus>(from))
      .HasDefaultValue(SubscriptionStatus.Active)
      // The CLR default of the enum is Inactive, so without the sentinel EF would swap an
      // explicit Inactive insert for the DB default; with it only Active omits the column
      .HasSentinel(SubscriptionStatus.Active);

    builder.Property(s => s.UserId)
      .IsRequired()
      .HasColumnType("uuid");

    builder
      .Property(s => s.Cooldown)
      .IsRequired()
      .HasColumnType("interval");

    builder
      .Property(s => s.Created)
      .IsRequired()
      .HasColumnType("timestamp with time zone")
      .HasDefaultValueSql("CURRENT_TIMESTAMP");

    builder
      .Property(s => s.Updated)
      .IsRequired(false)
      .HasColumnType("timestamp with time zone");

    builder
      .Property(s => s.LastTriggeredAt)
      .IsRequired(false)
      .HasColumnType("timestamp with time zone");

    builder
      .Property(s => s.LastReadAt)
      .IsRequired(false)
      .HasColumnType("timestamp with time zone");

    builder
      .Property(s => s.RuleGroupId)
      .IsRequired()
      .HasColumnType("uuid");

    // A subscription is a pure container of its rule; the rule cannot disappear while
    // subscriptions still reference it (the delete flows remove the subscriptions first)
    builder
      .HasOne<Rule>()
      .WithMany()
      .HasForeignKey(s => s.RuleGroupId)
      .OnDelete(DeleteBehavior.Restrict);

    builder
      .HasMany(s => s.Outputs)
      .WithOne()
      .HasForeignKey(o => o.SubscriptionId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.Ignore(s => s.CooldownSeconds);
  }
}
