using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockIndicators.Domain.Models.Users;

namespace StockIndicators.Infrastructure.Domain.Users;

internal class UserConfiguration : IEntityTypeConfiguration<User>
{
  public void Configure(EntityTypeBuilder<User> builder)
  {
    builder.HasKey(user => user.Id);

    builder
      .Property(user => user.Id)
      .IsRequired()
      .HasColumnType("uuid");

    builder
      .Property(user => user.Created)
      .IsRequired()
      .HasColumnType("timestamp with time zone")
      .HasDefaultValueSql("CURRENT_TIMESTAMP");

    builder
      .Property(user => user.Updated)
      .IsRequired(false)
      .HasColumnType("timestamp with time zone");

    builder
      .Property(user => user.Email)
      .IsRequired(false)
      .HasColumnType("varchar(255)");

    builder
      .Property(user => user.DisplayName)
      .IsRequired(false)
      .HasColumnType("varchar(255)");

    builder
      .Property(user => user.Language)
      .IsRequired(false)
      .HasColumnType("varchar(10)");

    builder
      .Property(user => user.Timezone)
      .IsRequired(false)
      .HasColumnType("varchar(64)");

    builder
      .Property(user => user.SubscriptionsSort)
      .IsRequired(false)
      .HasColumnType("varchar(32)");

    // jsonb (not varchar): the preferences document grows with new options without a migration
    builder
      .Property(user => user.ChartPreferences)
      .IsRequired(false)
      .HasColumnType("jsonb");

    builder
      .Property(user => user.InitOnboardingCompletedAt)
      .IsRequired(false)
      .HasColumnType("timestamp with time zone");

    builder
      .Property(user => user.SubscriptionOnboardingCompletedAt)
      .IsRequired(false)
      .HasColumnType("timestamp with time zone");

    builder
      .HasIndex(user => user.Email)
      .IsUnique()
      .HasFilter("\"Email\" IS NOT NULL");
  }
}
