using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Users;

namespace StockIndicators.Infrastructure.Domain.Rules;

public class RuleConfiguration : IEntityTypeConfiguration<Rule>
{
  public void Configure(EntityTypeBuilder<Rule> builder)
  {
    builder.HasKey(r => r.Id);

    builder
      .Property(r => r.Id)
      .IsRequired()
      .HasColumnType("uuid");

    builder
      .Property(r => r.UserId)
      .IsRequired()
      .HasColumnType("uuid");

    builder
      .Property(r => r.Name)
      .IsRequired()
      .HasColumnType("varchar(100)");

    builder
      .Property(r => r.RuleData)
      .IsRequired()
      .HasColumnType("jsonb");

    builder
      .Property(r => r.Created)
      .IsRequired()
      .HasColumnType("timestamp with time zone")
      .HasDefaultValueSql("CURRENT_TIMESTAMP");

    builder
      .Property(r => r.Updated)
      .IsRequired(false)
      .HasColumnType("timestamp with time zone");

    // Owning user; deleting the account cascades the user's rules
    // (the account deletion flow removes the user's subscriptions first — the
    // subscriptions-to-rule FK is Restrict, so nothing may reference a deleted rule)
    builder
      .HasOne<User>()
      .WithMany()
      .HasForeignKey(r => r.UserId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasIndex(r => r.UserId);

    // Deserialized by the repository layer from RuleData
    builder.Ignore(r => r.When);
    builder.Ignore(r => r.ThenTrue);
    builder.Ignore(r => r.ThenFalse);
  }
}
