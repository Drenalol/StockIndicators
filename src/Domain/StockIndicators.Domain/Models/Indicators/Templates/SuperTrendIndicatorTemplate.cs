using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "A trailing trend line built on ATR: price above the line - an uptrend, below - a downtrend. The line flips only on confirmed breaks, which filters out false breakouts. The alert fires when the trend direction flips.",
    Ru = "Трендовая линия, построенная на ATR: цена выше линии - восходящий тренд, ниже - нисходящий. Линия меняется только при подтверждённом развороте, что отсекает ложные пробои. Алерт срабатывает при смене направления тренда.",
    Es = "Una línea de tendencia móvil construida sobre el ATR: precio sobre la línea - tendencia alcista, bajo ella - bajista. La línea solo cambia con rupturas confirmadas, lo que filtra falsas rupturas. La alerta se activa cuando cambia la dirección de la tendencia.",
    It = "Una linea di tendenza con trailing stop basata sull'ATR: prezzo sopra la linea - trend rialzista, sotto - trend ribassista. La linea si inverte solo in caso di rotture confermate, il che filtra i falsi breakout. L'avviso scatta quando la direzione del trend cambia.",
    De = "Eine trailing Trendlinie auf ATR-Basis: Kurs über der Linie - Aufwärtstrend, darunter - Abwärtstrend. Die Linie wechselt nur bei bestätigten Ausbrüchen, was falsche Ausbrüche herausfiltert. Der Alert wird ausgelöst, wenn die Trendrichtung wechselt.",
    Fr = "Une ligne de tendance avec suivi de cours (trailing stop) construite sur l'ATR : prix au-dessus de la ligne - tendance haussière, en dessous - tendance baissière. La ligne ne bascule que sur des ruptures confirmées, ce qui filtre les fausses ruptures. L'alerte se déclenche lorsque la direction de la tendance s'inverse.",
    PtBr = "Uma linha de tendência com trailing stop construída sobre o ATR: preço acima da linha - tendência altista, abaixo - baixista. A linha só inverte em rupturas confirmadas, o que filtra falsas rupturas. O alerta é acionado quando a direção da tendência muda.",
    Uk = "Трендова лінія з трейлінг-стопом на основі ATR: ціна вище за лінію - висхідний тренд, нижче - низхідний. Лінія перемикається лише на підтверджених пробоях, що відсіює хибні пробої. Алерт спрацьовує, коли напрям тренду змінюється.",
    Be = "Трэндавая лінія з трэйл-стопам на аснове ATR: цана вышэй за лінію - узыходзячы трэнд, ніжэй - сляхадзячы. Лінія пераключаецца толькі пры пацверджаных прабоях, што адсявае ілжывыя прабоі. Апавяшчэнне спрацоўвае пры змене накірунку трэнду.")]
public class SuperTrendIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
{
  [ParameterHint(
      En = "Candle timeframe the indicator is calculated on. The alert is evaluated on each closed candle of this period.",
      Ru = "Таймфрейм свечей, по которым считается индикатор. Проверка выполняется при закрытии каждой свечи этого периода.",
      Es = "Temporalidad de las velas con la que se calcula el indicador. La alerta se evalúa al cierre de cada vela de este periodo.",
      It = "Timeframe delle candele su cui viene calcolato l'indicatore. L'avviso viene valutato alla chiusura di ogni candela di questo periodo.",
      De = "Zeitrahmen der Kerzen, auf dem der Indikator berechnet wird. Der Alert wird beim Schlusskurs jeder geschlossenen Kerze dieses Zeitraums ausgewertet.",
      Fr = "Unité de temps des bougies sur laquelle l'indicateur est calculé. L'alerte est évaluée à la clôture de chaque bougie de cette période.",
      PtBr = "Timeframe das velas em que o indicador é calculado. O alerta é avaliado no fechamento de cada vela desse período.",
      Uk = "Таймфрейм свічок, на якому розраховується індикатор. Алерт перевіряється на кожній закритій свічці цього періоду.",
      Be = "Таймфрэйм свічак, на якім разлічваецца індыкатар. Апавяшчэнне правяраецца пры закрыцці кожнай свічкі гэтага перыяду.")]
  public TimeFrame TimeFrame { get; set; }

  [ParameterHint(
      En = "ATR period used in the SuperTrend calculation. Standard is 10.",
      Ru = "Период ATR, используемый в расчёте SuperTrend. Стандарт - 10.",
      Es = "Periodo del ATR usado en el cálculo del SuperTrend. El estándar es 10.",
      It = "Periodo ATR utilizzato nel calcolo del SuperTrend. Lo standard è 10.",
      De = "ATR-Periode für die Berechnung des SuperTrend. Standardwert ist 10.",
      Fr = "Période ATR utilisée dans le calcul du SuperTrend. La valeur standard est 10.",
      PtBr = "Período do ATR usado no cálculo do SuperTrend. O padrão é 10.",
      Uk = "Період ATR, що використовується в розрахунку SuperTrend. Стандарт - 10.",
      Be = "Перыяд ATR, які выкарыстоўваецца ў разліку SuperTrend. Стандарт - 10.")]
  public int Period { get; set; } = 10;

  [ParameterHint(
      En = "Multiplier applied to ATR to build the trend line. A larger value means a more stable trend and fewer alerts. Standard is 3. The alert fires on a trend direction flip.",
      Ru = "Множитель к ATR для построения линии тренда. Чем больше значение, тем стабильнее тренд и реже алерты. Стандарт - 3. Алерт срабатывает при смене направления тренда.",
      Es = "Multiplicador aplicado al ATR para construir la línea de tendencia. Un valor mayor significa una tendencia más estable y menos alertas. El estándar es 3. La alerta se activa cuando cambia la dirección de la tendencia.",
      It = "Moltiplicatore applicato all'ATR per costruire la linea di trend. Un valore più elevato significa un trend più stabile e meno avvisi. Lo standard è 3. L'avviso scatta quando la direzione del trend cambia.",
      De = "Faktor, mit dem der ATR für die Trendlinie multipliziert wird. Ein größerer Wert bedeutet einen stabileren Trend und weniger Alerts. Standard ist 3. Der Alert wird bei einem Richtungswechsel des Trends ausgelöst.",
      Fr = "Multiplicateur appliqué à l'ATR pour construire la ligne de tendance. Une valeur plus élevée signifie une tendance plus stable et moins d'alertes. La valeur standard est 3. L'alerte se déclenche lorsque la direction de la tendance s'inverse.",
      PtBr = "Multiplicador aplicado ao ATR para construir a linha de tendência. Um valor maior significa uma tendência mais estável e menos alertas. O padrão é 3. O alerta é acionado quando a direção da tendência muda.",
      Uk = "Множник до ATR для побудови лінії тренду. Чим більше значення, тим стабільніший тренд і рідше спрацьовують алерти. Стандарт - 3. Алерт спрацьовує при зміні напряму тренду.",
      Be = "Множнік да ATR для пабудовы лініі трэнду. Чым большае значэнне, тым больш устойлівы трэнд і радзей спрацоўваюць апавяшчэнні. Стандарт - 3. Апавяшчэнне спрацоўвае пры змене накірунку трэнду.")]
  public double Multiplier { get; set; } = 3;

  public int GetCandleCount() => Period * 3;
}
