using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockIndicators.Domain.Models.Telegram;

namespace StockIndicators.Infrastructure.Domain.Telegram;

internal class TelegramChatConfiguration : IEntityTypeConfiguration<TelegramChat>
{
  public void Configure(EntityTypeBuilder<TelegramChat> builder)
  {
    builder.HasKey(chat => chat.Id);

    builder
      .Property(chat => chat.Id)
      .IsRequired()
      .HasColumnType("uuid");

    builder
      .Property(chat => chat.Created)
      .IsRequired()
      .HasColumnType("timestamp with time zone")
      .HasDefaultValueSql("CURRENT_TIMESTAMP");

    builder
      .Property(chat => chat.Updated)
      .IsRequired(false)
      .HasColumnType("timestamp with time zone");

    builder
      .Property(chat => chat.UserId)
      .IsRequired()
      .HasColumnType("uuid");

    builder
      .Property(chat => chat.ChatId)
      .IsRequired()
      .HasColumnType("bigint");

    builder
      .Property(chat => chat.Username)
      .IsRequired(false)
      .HasColumnType("varchar(64)");

    builder
      .HasOne(chat => chat.User)
      .WithMany()
      .HasForeignKey(chat => chat.UserId)
      .OnDelete(DeleteBehavior.Cascade);

    builder
      .HasIndex(chat => chat.ChatId)
      .IsUnique();

    // An account has at most one Telegram chat: linking a new one replaces the previous
    builder
      .HasIndex(chat => chat.UserId)
      .IsUnique();
  }
}
