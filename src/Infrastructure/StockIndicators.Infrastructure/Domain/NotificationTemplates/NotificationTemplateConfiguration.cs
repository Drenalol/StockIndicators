using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockIndicators.Domain.Models.NotificationTemplates;

namespace StockIndicators.Infrastructure.Domain.NotificationTemplates;

public class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
  public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
  {
    builder.HasKey(nt => nt.Id);

    builder
      .Property(nt => nt.Id)
      .IsRequired()
      .HasColumnType("uuid");

    builder
      .Property(nt => nt.Name)
      .IsRequired()
      .HasColumnType("varchar(50)");

    builder
      .Property(nt => nt.IsBundled)
      .IsRequired()
      .HasColumnType("boolean")
      .HasDefaultValue(false);

    builder
      .Property(nt => nt.Template)
      .IsRequired()
      .HasColumnType("text");
    
    builder
      .Property(nt => nt.Created)
      .IsRequired()
      .HasColumnType("timestamp with time zone")
      .HasDefaultValueSql("CURRENT_TIMESTAMP");
    
    builder
      .Property(nt => nt.Updated)
      .IsRequired(false)
      .HasColumnType("timestamp with time zone");

    // Owner of a user template. Null for bundled templates and legacy rows (visible to everyone)
    builder
      .Property(nt => nt.UserId)
      .IsRequired(false)
      .HasColumnType("uuid");
  }
}