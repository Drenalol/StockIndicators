using System.ComponentModel.DataAnnotations;

namespace StockIndicators.Providers.Base.Exchange;

public class ExchangeSessionOptions
{
  [Required]
  public string TimeZoneId { get; set; } = "UTC";

  public ExchangeSessionWindow[] Sessions { get; set; } = [];

  public ExchangeSessionWindow? PreMarket { get; set; }

  public ExchangeSessionWindow? PostMarket { get; set; }

  /// <summary>The 20:00-04:00 overnight window (crosses midnight); configured only if a provider ever ships that feed.</summary>
  public ExchangeSessionWindow? Overnight { get; set; }

  public DayOfWeek[] WeekendDays { get; set; } = [DayOfWeek.Saturday, DayOfWeek.Sunday];

  public DateOnly[] Holidays { get; set; } = [];
}
