using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "Measures the strength of a trend regardless of its direction: above 25 the trend is considered strong, below 20 - weak or ranging. The +DI/-DI lines show which side is dominant. The alert fires when ADX crosses the threshold in either direction.",
    Ru = "Показывает силу тренда независимо от направления: выше 25 тренд считается сильным, ниже 20 - слабым или боковым. Линии +DI/-DI показывают, какая сторона доминирует. Алерт срабатывает при пересечении ADX порога в любую сторону.",
    Es = "Mide la fuerza de una tendencia independientemente de su dirección: por encima de 25 la tendencia se considera fuerte, por debajo de 20 - débil o lateral. Las líneas +DI/-DI muestran qué lado domina. La alerta se activa cuando el ADX cruza el umbral en cualquier dirección.",
    It = "Misura la forza di un trend indipendentemente dalla sua direzione: sopra 25 il trend è considerato forte, sotto 20 - debole o laterale. Le linee +DI/-DI mostrano quale lato domina. L'avviso scatta quando l'ADX incrocia la soglia in qualsiasi direzione.",
    De = "Misst die Stärke eines Trends unabhängig von seiner Richtung: über 25 gilt der Trend als stark, unter 20 - schwach oder seitwärts. Die +DI/-DI-Linien zeigen, welche Seite dominiert. Der Alert wird ausgelöst, wenn der ADX den Schwellenwert in beide Richtungen kreuzt.",
    Fr = "Mesure la force d'une tendance indépendamment de sa direction : au-dessus de 25 la tendance est considérée forte, en dessous de 20 - faible ou en range. Les lignes +DI/-DI montrent quel côté domine. L'alerte se déclenche quand l'ADX croise le seuil dans n'importe quelle direction.",
    PtBr = "Mede a força de uma tendência independentemente da direção: acima de 25 a tendência é considerada forte, abaixo de 20 - fraca ou lateral. As linhas +DI/-DI mostram qual lado domina. O alerta é acionado quando o ADX cruza o limiar em qualquer direção.",
    Uk = "Вимірює силу тренду незалежно від напрямку: вище 25 тренд вважається сильним, нижче 20 - слабким або бічним. Лінії +DI/-DI показують, який бік домінує. Алерт спрацьовує, коли ADX перетинає поріг у будь-якому напрямку.",
    Be = "Вымярае сілу трэнду незалежна ад накірунку: вышэй 25 трэнд лічыцца моцным, ніжэй 20 - слабым або бакавым. Лініі +DI/-DI паказваюць, які бок дамінуе. Апавяшчэнне спрацоўвае, калі ADX перасякае парог у любым накірунку.")]
public class AdxIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
{
  [ParameterHint(
      En = "Candle timeframe the indicator is calculated on. The alert is evaluated on each closed candle of this period.",
      Ru = "Таймфрейм свечей, по которым считается индикатор. Проверка выполняется при закрытии каждой свечи этого периода.",
      Es = "Temporalidad de las velas con la que se calcula el indicador. La alerta se evalúa al cierre de cada vela de este periodo.",
      It = "Timeframe delle candele su cui viene calcolato l'indicatore. L'avviso viene valutato alla chiusura di ogni candela di questo periodo.",
      De = "Zeitrahmen der Kerzen, für den der Indikator berechnet wird. Der Alert wird bei jeder geschlossenen Kerze dieses Zeitraums ausgewertet.",
      Fr = "Unité de temps des bougies sur laquelle l'indicateur est calculé. L'alerte est évaluée à la clôture de chaque bougie de cette période.",
      PtBr = "Timeframe das velas no qual o indicador é calculado. O alerta é avaliado no fechamento de cada vela deste período.",
      Uk = "Таймфрейм свічок, за яким розраховується індикатор. Алерт перевіряється при закритті кожної свічки цього періоду.",
      Be = "Таймфрейм свічак, па якіх разлічваецца індыкатар. Апавяшчэнне праверяецца пры закрыцці кожнай свічкі гэтага перыяду.")]
  public TimeFrame TimeFrame { get; set; }

  [ParameterHint(
      En = "ADX calculation period in candles. Standard is 14.",
      Ru = "Период расчёта ADX в свечах. Стандарт - 14.",
      Es = "Periodo de cálculo del ADX en velas. El estándar es 14.",
      It = "Periodo di calcolo dell'ADX in candele. Lo standard è 14.",
      De = "ADX-Berechnungszeitraum in Kerzen. Standard ist 14.",
      Fr = "Période de calcul de l'ADX en bougies. La valeur standard est 14.",
      PtBr = "Período de cálculo do ADX em velas. O padrão é 14.",
      Uk = "Період розрахунку ADX у свічках. Стандарт - 14.",
      Be = "Перыяд разліку ADX у свачках. Стандарт - 14.")]
  public int Period { get; set; } = 14;

  [ParameterHint(
      En = "The alert fires when ADX crosses this level in either direction. 25 is considered a strong trend, 20 - a moderate one.",
      Ru = "Алерт срабатывает при пересечении ADX этого уровня в любую сторону. 25 считается сильным трендом, 20 - умеренным.",
      Es = "La alerta se activa cuando el ADX cruza este nivel en cualquier dirección. 25 se considera tendencia fuerte, 20 - moderada.",
      It = "L'avviso scatta quando l'ADX incrocia questo livello in qualsiasi direzione. 25 è considerato un trend forte, 20 - moderato.",
      De = "Der Alert wird ausgelöst, wenn der ADX diese Marke in beide Richtungen kreuzt. 25 gilt als starker Trend, 20 - als moderater.",
      Fr = "L'alerte se déclenche quand l'ADX croise ce niveau dans n'importe quelle direction. 25 est considéré comme une tendance forte, 20 - comme une tendance modérée.",
      PtBr = "O alerta é acionado quando o ADX cruza este nível em qualquer direção. 25 é considerado tendência forte, 20 - moderada.",
      Uk = "Алерт спрацьовує, коли ADX перетинає цей рівень у будь-якому напрямку. 25 вважається сильним трендом, 20 - помірним.",
      Be = "Апавяшчэнне спрацоўвае, калі ADX перасякае гэты ўзровень у любым накірунку. 25 лічыцца моцным трэндам, 20 - умераным.")]
  public double AdxThreshold { get; set; } = 25;

  public int GetCandleCount() => Period * 3;
}
