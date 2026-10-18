using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "A volatility channel built on an EMA centerline with ATR-based band widths: the bands hug the average and expand with volatility. Price outside the bands marks an unusual move for the recent range. The alert fires when the close leaves or returns into the channel.",
    Ru = "Канал волатильности вокруг экспоненциальной средней с шириной полос по ATR: полосы огибают среднюю и расширяются при росте волатильности. Цена вне полос отмечает необычное движение для недавнего диапазона. Алерт срабатывает, когда закрытие выходит из канала или возвращается в него.",
    Es = "Un canal de volatilidad construido sobre una línea central EMA con anchos de banda basados en el ATR: las bandas abrazan la media y se amplían con la volatilidad. El precio fuera de las bandas marca un movimiento inusual para el rango reciente. La alerta se activa cuando el cierre sale del canal o vuelve a él.",
    It = "Un canale di volatilità costruito su una linea centrale EMA con ampiezze delle bande basate sull'ATR: le bande avvolgono la media e si allargano con la volatilità. Il prezzo fuori dalle bande segnala un movimento insolito per il range recente. L'avviso scatta quando la chiusura esce dal canale o vi fa ritorno.",
    De = "Ein Volatilitätskanal auf Basis einer EMA-Mittellinie mit ATR-basierten Bandbreiten: die Bänder umschließen den Durchschnitt und weiten sich mit der Volatilität. Ein Kurs außerhalb der Bänder markiert eine ungewöhnliche Bewegung für die jüngere Spanne. Der Alert wird ausgelöst, wenn der Schlusskurs den Kanal verlässt oder in ihn zurückkehrt.",
    Fr = "Un canal de volatilité construit sur une ligne médiane EMA avec des largeurs de bande basées sur l'ATR : les bandes épousent la moyenne et s'élargissent avec la volatilité. Un prix hors des bandes signale un mouvement inhabituel pour la plage récente. L'alerte se déclenche lorsque la clôture sort du canal ou y revient.",
    PtBr = "Um canal de volatilidade construído sobre uma linha central EMA com larguras de banda baseadas no ATR: as bandas abraçam a média e se expandem com a volatilidade. O preço fora das bandas marca um movimento incomum para o range recente. O alerta é acionado quando o fechamento sai do canal ou retorna a ele.",
    Uk = "Канал волатильності навколо експоненційної ковзної середньої (EMA) із шириною смуг на основі ATR: смуги огортають середню і розширюються зі зростанням волатильності. Ціна поза смугами позначає незвичний рух для останнього діапазону. Алерт спрацьовує, коли закриття виходить з каналу чи повертається в нього.",
    Be = "Канал валятыльнасці вакол экспанэнцыяльнай слізгальнай сярэдняй (EMA) з шырынёй палос на аснове ATR: паласы ахопліваюць сярэднюю і пашыраюцца з ростам валятыльнасці. Цэна па-за палосамі адзначае незвычайны рух для нядаўняга дыяпазону. Апавяшчэнне спрацоўвае, калі закрыццё выходзіць з каналу ці вяртаецца ў яго.")]
public class KeltnerIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
{
  [ParameterHint(
      En = "Candle timeframe the indicator is calculated on. The alert is evaluated on each closed candle of this period.",
      Ru = "Таймфрейм свечей, по которым считается индикатор. Проверка выполняется при закрытии каждой свечи этого периода.",
      Es = "Temporalidad de las velas con la que se calcula el indicador. La alerta se evalúa al cierre de cada vela de este periodo.",
      It = "Timeframe delle candele su cui viene calcolato l'indicatore. L'avviso viene valutato alla chiusura di ogni candela di questo periodo.",
      De = "Zeitrahmen der Kerzen, auf dem der Indikator berechnet wird. Der Alert wird beim Schluss jeder Kerze dieses Zeitraums ausgewertet.",
      Fr = "Unité de temps des bougies sur laquelle l'indicateur est calculé. L'alerte est évaluée à la clôture de chaque bougie de cette période.",
      PtBr = "Timeframe das velas usado para calcular o indicador. O alerta é avaliado no fechamento de cada vela desse período.",
      Uk = "Таймфрейм свічок, на якому обчислюється індикатор. Алерт перевіряється на кожній закритій свічці цього періоду.",
      Be = "Таймфрейм свічак, на якім вылічваецца індыкатар. Апавяшчэнне правяраецца на кожнай закрытай свічцы гэтага перыяду.")]
  public TimeFrame TimeFrame { get; set; }

  [ParameterHint(
      En = "Period of the EMA centerline and, unless a separate ATR period is set, of the ATR as well. Standard is 20.",
      Ru = "Период экспоненциальной средней, и - если не задан отдельный период ATR - тоже ATR. Стандарт - 20.",
      Es = "Periodo de la línea central EMA y, salvo que se fije un periodo de ATR aparte, también del ATR. El estándar es 20.",
      It = "Periodo della linea centrale EMA e, a meno che non sia impostato un periodo ATR separato, anche dell'ATR. Lo standard è 20.",
      De = "Periode der EMA-Mittellinie und, sofern kein separater ATR-Zeitraum festgelegt ist, auch des ATR. Standard ist 20.",
      Fr = "Période de la ligne médiane EMA et, sauf période ATR distincte définie, également de l'ATR. Le standard est 20.",
      PtBr = "Período da linha central EMA e, a menos que um período de ATR separado seja definido, também do ATR. O padrão é 20.",
      Uk = "Період центральної лінії EMA і, якщо не задано окремий період ATR, також період ATR. Стандарт - 20.",
      Be = "Перыяд цэнтральнай лініі EMA і, калі не зададзены асобны перыяд ATR, таксама перыяд ATR. Стандарт - 20.")]
  public int Period { get; set; } = 20;

  [ParameterHint(
      En = "Period of the ATR that sets the band widths. Standard is 10.",
      Ru = "Период ATR, задающий ширину полос. Стандарт - 10.",
      Es = "Periodo del ATR que fija los anchos de las bandas. El estándar es 10.",
      It = "Periodo dell'ATR che determina le ampiezze delle bande. Lo standard è 10.",
      De = "Periode des ATR, der die Bandbreiten festlegt. Standard ist 10.",
      Fr = "Période de l'ATR qui fixe les largeurs de bande. Le standard est 10.",
      PtBr = "Período do ATR que define as larguras das bandas. O padrão é 10.",
      Uk = "Період ATR, що задає ширину смуг. Стандарт - 10.",
      Be = "Перыяд ATR, які задае шырыню палос. Стандарт - 10.")]
  public int AtrPeriod { get; set; } = 10;

  [ParameterHint(
      En = "Multiplier applied to the ATR to build the bands. A larger value means wider bands and rarer alerts. Standard is 2.",
      Ru = "Множитель ATR для построения полос. Чем больше значение, тем шире полосы и реже алерты. Стандарт - 2.",
      Es = "Multiplicador aplicado al ATR para construir las bandas. Un valor mayor significa bandas más anchas y alertas más raras. El estándar es 2.",
      It = "Moltiplicatore applicato all'ATR per costruire le bande. Un valore più ampio significa bande più larghe e avvisi più rari. Lo standard è 2.",
      De = "Multiplikator, der auf den ATR angewendet wird, um die Bänder zu bilden. Ein größerer Wert bedeutet breitere Bänder und seltenere Alerts. Standard ist 2.",
      Fr = "Multiplicateur appliqué à l'ATR pour construire les bandes. Une valeur plus élevée donne des bandes plus larges et des alertes plus rares. Le standard est 2.",
      PtBr = "Multiplicador aplicado ao ATR para construir as bandas. Um valor maior significa bandas mais largas e alertas mais raros. O padrão é 2.",
      Uk = "Множник ATR для побудови смуг. Чим більше значення, тим ширші смуги і рідші алерти. Стандарт - 2.",
      Be = "Множнік ATR для пабудовы палос. Чым большае значэнне, тым шырэйшыя палосы і радкавейшыя апавяшчэнні. Стандарт - 2.")]
  public double Multiplier { get; set; } = 2.0;
  public int GetCandleCount() => Math.Max(Period, AtrPeriod) * 2;
}
