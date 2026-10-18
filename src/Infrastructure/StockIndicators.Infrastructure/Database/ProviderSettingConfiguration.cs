using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockIndicators.Domain.Models.Providers;

namespace StockIndicators.Infrastructure.Database;

public class ProviderSettingConfiguration : IEntityTypeConfiguration<ProviderSetting>
{
  public void Configure(EntityTypeBuilder<ProviderSetting> builder)
  {
    builder.ToTable("ProviderSettings");

    builder.HasKey(setting => setting.Id);

    // The id is the provider code, hand-picked by the registry - never generated
    builder.Property(setting => setting.Id).HasMaxLength(64).ValueGeneratedNever();

    builder.Property(setting => setting.Credentials).HasColumnType("jsonb");
  }
}
