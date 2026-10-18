using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "Two lines that measure the forward and backward price movement over the window (VI+ and VI-), staying around the 100 level: when one rises above the other, that direction dominates. A wide gap between the lines marks a strong trend. The alert fires when VI+ and VI- cross.",
    Ru = "Две линии, измеряющие движение цены вперёд и назад за окно (VI+ и VI-), колеблющиеся около уровня 100: когда одна поднимается выше другой, это направление доминирует. Большой разрыв между линиями отмечает сильный тренд. Алерт срабатывает при пересечении VI+ и VI-.",
    Es = "Dos líneas que miden el movimiento del precio hacia adelante y hacia atrás en la ventana (VI+ y VI-), oscilando en torno al nivel 100: cuando una sube sobre la otra, esa dirección domina. Una brecha amplia entre las líneas marca una tendencia fuerte. La alerta se activa cuando VI+ y VI- se cruzan.",
    It = "Due linee che misurano il movimento del prezzo in avanti e all'indietro sulla finestra (VI+ e VI-), restando intorno al livello 100: quando una sale sopra l'altra, quella direzione domina. Un ampio divario tra le linee indica un trend forte. L'avviso scatta quando VI+ e VI- si incrociano.",
    De = "Zwei Linien, die die Vorwärts- und Rückwärtsbewegung des Kurses im Fenster messen und um den 100er-Pegel bleiben: Wenn eine über die andere steigt, dominiert diese Richtung. Eine breite Lücke zwischen den Linien markiert einen starken Trend. Der Alert wird ausgelöst, wenn sich VI+ und VI- kreuzen.",
    Fr = "Deux lignes qui mesurent le mouvement du prix vers l'avant et vers l'arrière sur la fenêtre (VI+ et VI-), évoluant autour du niveau 100 : quand l'une dépasse l'autre, cette direction domine. Un large écart entre les lignes marque une tendance forte. L'alerte se déclenche quand VI+ et VI- se croisent.",
    PtBr = "Duas linhas que medem o movimento do preço para frente e para trás na janela (VI+ e VI-), ficando em torno do nível 100: quando uma sobe acima da outra, essa direção domina. Um afastamento largo entre as linhas marca uma tendência forte. O alerta é acionado quando VI+ e VI- se cruzam.",
    Uk = "Дві лінії, що вимірюють рух ціни вперед і назад за вікно (VI+ та VI-), вагаючись біля рівня 100: коли одна піднімається вище за іншу, цей напрям домінує. Широкий розрив між лініями позначає сильний тренд. Алерт спрацьовує при перетині VI+ та VI-.",
    Be = "Дзве лініі, якія вымяраюць рух цаны наперад і назад за акно (VI+ і VI-), трымаючыся вакол узроўню 100: калі адна падымаецца вышэй за другую, гэты накірунак дамінуе. Шырокі разрыў паміж лініямі паказвае моцны трэнд. Апавяшчэнне спрацоўвае пры перасячэнні VI+ і VI-.")]
public class VortexIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
      En = "Window in candles over which the positive and negative movements are summed. Standard is 14.",
      Ru = "Окно в свечах, за которое суммируются положительное и отрицательное движения. Стандарт - 14.",
      Es = "Ventana en velas sobre la que se suman los movimientos positivo y negativo. El estándar es 14.",
      It = "Finestra in candele su cui vengono sommati i movimenti positivi e negativi. Lo standard è 14.",
      De = "Fenster in Kerzen, über das die Aufwärts- und Abwärtsbewegungen summiert werden. Standard ist 14.",
      Fr = "Fenêtre en bougies sur laquelle les mouvements positifs et négatifs sont sommés. La valeur standard est 14.",
      PtBr = "Janela em velas sobre a qual os movimentos positivos e negativos são somados. O padrão é 14.",
      Uk = "Вікно в свічках, за яке сумуються позитивний і негативний рухи. Стандарт - 14.",
      Be = "Акно ў свечках, за якое сумуюцца дадатны і адмоўны рухі. Стандарт - 14.")]
  public int Period { get; set; } = 14;
  public int GetCandleCount() => Period * 2;
}
