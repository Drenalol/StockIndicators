using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "A moving average weighted by volume: each candle counts in proportion to its traded volume, so the line follows the price where the money actually changed hands. High-volume levels pull the line, thin candles barely move it. The alert fires when a candle crosses the average within the deviation band.",
    Ru = "Скользящая средняя, взвешенная по объёму: каждая свеча участвует пропорционально своему объёму, поэтому линия следует цене там, где реально шли торги. Объёмные уровни тянут линию за собой, тонкие свечи её почти не сдвигают. Алерт срабатывает, когда свеча пересекает среднюю в пределах полосы допуска.",
    Es = "Una media móvil ponderada por volumen: cada vela cuenta en proporción al volumen negociado, por lo que la línea sigue el precio donde el dinero cambió realmente de manos. Los niveles de alto volumen atraen la línea, las velas finas apenas la mueven. La alerta se activa cuando una vela cruza la media dentro de la banda de tolerancia.",
    It = "Una media mobile ponderata sul volume: ogni candela conta in proporzione al volume scambiato, quindi la linea segue il prezzo dove il denaro ha realmente cambiato mano. I livelli ad alto volume attirano la linea, le candele sottili la spostano a malapena. L'avviso scatta quando una candela incrocia la media entro la banda di deviazione.",
    De = "Ein gleitender Durchschnitt, gewichtet nach dem Volumen: Jede Kerze zählt im Verhältnis zu ihrem gehandelten Volumen, daher folgt die Linie dem Kurs dort, wo das Geld tatsächlich den Besitzer gewechselt hat. Niveaus mit hohem Volumen ziehen die Linie an, dünne Kerzen bewegen sie kaum. Der Alert wird ausgelöst, wenn eine Kerze den Durchschnitt innerhalb des Abweichungsbands kreuzt.",
    Fr = "Une moyenne mobile pondérée par le volume : chaque bougie compte proportionnellement à son volume échangé, donc la ligne suit le prix là où l'argent a réellement changé de main. Les niveaux à fort volume attirent la ligne, les bougies peu épaisses la déplacent à peine. L'alerte se déclenche quand une bougie croise la moyenne à l'intérieur de la bande d'écart.",
    PtBr = "Uma média móvel ponderada por volume: cada vela conta proporcionalmente ao seu volume negociado, então a linha segue o preço onde o dinheiro realmente trocou de mãos. Níveis de alto volume puxam a linha, velas finas mal a movem. O alerta é acionado quando uma vela cruza a média dentro da banda de desvio.",
    Uk = "Ковзна середня, зважена за обсягом: кожна свічка враховується пропорційно до свого обсягу торгів, тож лінія йде за ціною там, де реально йшли торги. Об'ємні рівні тягнуть лінію за собою, тонкі свічки ледь зсувають її. Алерт спрацьовує, коли свічка перетинає середню в межах смуги відхилення.",
    Be = "Слізгальная сярэдняя, узважаная па аб'ёме: кожная свечка ўлічваецца прапарцыйна свайму аб'ёму гандлю, таму лінія ідзе за цаной там, дзе сапраўды ішоў гандаль. Аб'ёмныя ўзроўні цягнуць лінію за сабой, тонкія свечкі ледзь зрушваюць яе. Апавяшчэнне спрацоўвае, калі свечка перасякае сярэднюю ў межах паласы адхілення.")]
public class VwmaIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
      En = "Number of candles used to calculate the volume-weighted average.",
      Ru = "Сколько свечей участвует в расчёте объёмно-взвешенной средней.",
      Es = "Número de velas utilizadas para calcular la media ponderada por volumen.",
      It = "Numero di candele utilizzate per calcolare la media ponderata sul volume.",
      De = "Anzahl der Kerzen zur Berechnung des volumengewichteten Durchschnitts.",
      Fr = "Nombre de bougies utilisées pour calculer la moyenne pondérée par le volume.",
      PtBr = "Número de velas usadas para calcular a média ponderada por volume.",
      Uk = "Кількість свічок, що використовуються для розрахунку об'ємно-зваженої середньої.",
      Be = "Колькасць свечак для разліку аб'ёмна-ўзважанай сярэдняй.")]
  public int Periods { get; set; } = 20;

  [ParameterHint(
      En = "Tolerance for detecting a price cross of the average, as a fraction of its value (0.0005 = 0.05%). The alert fires when the price crosses the average within this band.",
      Ru = "Допуск при определении пересечения цены средней, в доле от её значения (0.0005 = 0.05%). Алерт срабатывает, когда цена пересекает среднюю в пределах этой полосы.",
      Es = "Tolerancia para detectar el cruce del precio con la media, como fracción de su valor (0.0005 = 0.05%). La alerta se activa cuando el precio cruza la media dentro de esta banda.",
      It = "Tolleranza per rilevare l'incrocio del prezzo con la media, come frazione del suo valore (0.0005 = 0.05%). L'avviso scatta quando il prezzo incrocia la media entro questa banda.",
      De = "Toleranz für die Erkennung einer Kreuzung des Kurses mit dem Durchschnitt, als Bruchteil seines Werts (0.0005 = 0,05 %). Der Alert wird ausgelöst, wenn der Kurs den Durchschnitt innerhalb dieses Bands kreuzt.",
      Fr = "Tolérance pour détecter le croisement du prix avec la moyenne, en fraction de sa valeur (0.0005 = 0.05 %). L'alerte se déclenche quand le prix croise la moyenne à l'intérieur de cette bande.",
      PtBr = "Tolerância para detectar o cruzamento do preço com a média, como fração do seu valor (0.0005 = 0.05%). O alerta é acionado quando o preço cruza a média dentro dessa banda.",
      Uk = "Допуск для виявлення перетину середньої ціною, як частка її значення (0.0005 = 0.05%). Алерт спрацьовує, коли ціна перетинає середню в межах цієї смуги.",
      Be = "Дапуск для выяўлення перасячэння цаной сярэдняй, як частка яе значэння (0.0005 = 0.05%). Апавяшчэнне спрацоўвае, калі цана перасякае сярэднюю ў межах гэтай паласы.")]
  public double Deviation { get; set; } = 0.0005F;

  public int GetCandleCount() => Periods;
}
