using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "The average true range over N candles - a pure volatility measure, not a direction. A rising ATR means the market is moving more. The alert fires when ATR as a percent of price reaches the minimum value.",
    Ru = "Средний истинный диапазон за N свечей - чистая мера волатильности, а не направления. Рост ATR означает, что рынок двигается сильнее. Алерт срабатывает, когда ATR в процентах от цены достигает минимального значения.",
    Es = "El rango verdadero medio durante N velas - una medida pura de volatilidad, no de dirección. Un ATR al alza significa que el mercado se mueve más. La alerta se activa cuando el ATR como porcentaje del precio alcanza el valor mínimo.",
    It = "L'average true range su N candele - una misura pura di volatilità, non di direzione. Un ATR in crescita significa che il mercato si muove di più. L'avviso scatta quando l'ATR in percentuale sul prezzo raggiunge il valore minimo.",
    De = "Die Average True Range über N Kerzen - ein reines Maß der Volatilität, nicht der Richtung. Ein steigender ATR bedeutet, dass sich der Markt stärker bewegt. Der Alert wird ausgelöst, wenn der ATR als Prozent des Kurses den Mindestwert erreicht.",
    Fr = "L'average true range sur N bougies - une mesure pure de la volatilité, non de la direction. Un ATR en hausse signifie que le marché bouge davantage. L'alerte se déclenche quand l'ATR en pourcentage du cours atteint la valeur minimale.",
    PtBr = "O average true range por N velas - uma medida pura de volatilidade, não de direção. Um ATR em alta significa que o mercado se move mais. O alerta é acionado quando o ATR como percentual do preço atinge o valor mínimo.",
    Uk = "Середній істинний діапазон за N свічок - чистий вимір волатильності, а не напрямку. Зростання ATR означає, що ринок рухається сильніше. Алерт спрацьовує, коли ATR у відсотках від ціни досягає мінімального значення.",
    Be = "Сярэдні ісцінны дыяпазон за N свачак - чыстая мяра валятыльнасці, а не накірунку. Рост ATR азначае, што рынак рухаецца мацней. Апавяшчэнне спрацоўвае, калі ATR у адсотках ад цаны дасягае мінімальнага значэння.")]
public class AtrIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
      En = "ATR calculation period in candles. Standard is 14.",
      Ru = "Период расчёта ATR в свечах. Стандарт - 14.",
      Es = "Periodo de cálculo del ATR en velas. El estándar es 14.",
      It = "Periodo di calcolo dell'ATR in candele. Lo standard è 14.",
      De = "ATR-Berechnungszeitraum in Kerzen. Standard ist 14.",
      Fr = "Période de calcul de l'ATR en bougies. La valeur standard est 14.",
      PtBr = "Período de cálculo do ATR em velas. O padrão é 14.",
      Uk = "Період розрахунку ATR у свічках. Стандарт - 14.",
      Be = "Перыяд разліку ATR у свачках. Стандарт - 14.")]
  public int Period { get; set; } = 14;

  [ParameterHint(
      En = "Minimum ATR as a percent of the current price. The alert fires when volatility is at least this value.",
      Ru = "Минимальное значение ATR в процентах от текущей цены. Алерт срабатывает, когда волатильность не ниже этого значения.",
      Es = "ATR mínimo como porcentaje del precio actual. La alerta se activa cuando la volatilidad es al menos este valor.",
      It = "ATR minimo in percentuale sul prezzo attuale. L'avviso scatta quando la volatilità è almeno pari a questo valore.",
      De = "Mindest-ATR als Prozent des aktuellen Kurses. Der Alert wird ausgelöst, wenn die Volatilität mindestens diesen Wert erreicht.",
      Fr = "ATR minimal en pourcentage du cours actuel. L'alerte se déclenche quand la volatilité atteint au moins cette valeur.",
      PtBr = "ATR mínimo como percentual do preço atual. O alerta é acionado quando a volatilidade atinge pelo menos esse valor.",
      Uk = "Мінімальний ATR у відсотках від поточної ціни. Алерт спрацьовує, коли волатильність не нижча за це значення.",
      Be = "Мінімальны ATR у адсотках ад бягучай цаны. Апавяшчэнне спрацоўвае, калі валятыльнасць не ніжэйшая за гэтае значэнне.")]
  public double MinPercent { get; set; } = 2;

  public int GetCandleCount() => Period * 2;
}
