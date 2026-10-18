using Microsoft.EntityFrameworkCore;
using StockIndicators.Domain.Models.AppSettings;
using StockIndicators.Domain.Models.Indicators;
using StockIndicators.Domain.Models.Instruments;
using StockIndicators.Domain.Models.NotificationTemplates;
using StockIndicators.Domain.Models.Providers;
using StockIndicators.Domain.Models.PushSubscriptions;
using StockIndicators.Domain.Models.Rules;
using StockIndicators.Domain.Models.Subscriptions;
using StockIndicators.Domain.Models.Telegram;
using StockIndicators.Domain.Models.Users;

namespace StockIndicators.Infrastructure.Database;

public class StockIndicatorsContext : DbContext
{
  public DbSet<User> Users { get; set; } = null!;
  public DbSet<TelegramChat> TelegramChats { get; set; } = null!;
  public DbSet<Subscription> Subscriptions { get; set; } = null!;
  public DbSet<NotificationTemplate> NotificationTemplates { get; set; } = null!;
  public DbSet<TriggerHistory> TriggerHistory { get; set; } = null!;
  public DbSet<SubscriptionOutput> SubscriptionOutputs { get; set; } = null!;
  public DbSet<PushSubscription> PushSubscriptions { get; set; } = null!;
  public DbSet<Rule> Rules { get; set; } = null!;
  public DbSet<RuleInstrument> RuleInstruments { get; set; } = null!;
  public DbSet<RuleIndicator> RuleIndicators { get; set; } = null!;
  public DbSet<TriggerSnapshot> TriggerSnapshots { get; set; } = null!;
  public DbSet<TriggerDelivery> TriggerDeliveries { get; set; } = null!;
  public DbSet<TrailingStopAnchor> TrailingStopAnchors { get; set; } = null!;
  public DbSet<Instrument> Instruments { get; set; } = null!;
  public DbSet<ProviderSetting> ProviderSettings { get; set; } = null!;

  public DbSet<AppSetting> AppSettings { get; set; } = null!;

  public StockIndicatorsContext(DbContextOptions<StockIndicatorsContext> options) : base(options)
  {
  }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(StockIndicatorsContext).Assembly);
  }
}
