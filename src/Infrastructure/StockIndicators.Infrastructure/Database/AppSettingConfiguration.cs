using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockIndicators.Domain.Models.AppSettings;

namespace StockIndicators.Infrastructure.Database;

public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
  public void Configure(EntityTypeBuilder<AppSetting> builder)
  {
    builder.ToTable("AppSettings");

    builder.HasKey(setting => setting.Id);

    // the key is a feature-chosen name ("onboarding"), never generated
    builder.Property(setting => setting.Id).HasMaxLength(64).ValueGeneratedNever();

    builder.Property(setting => setting.Value).HasColumnType("jsonb");
  }
}
