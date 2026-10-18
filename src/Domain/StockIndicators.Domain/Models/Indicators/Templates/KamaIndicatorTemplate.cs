using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "Kaufman's Adaptive Moving Average: the smoothing speed adapts to market noise - in a quiet, sideways market the line freezes and ignores noise, and once a real trend starts it quickly speeds up toward the fast average. A cross of the line marks a change that survived the noise filter. The alert fires when a candle crosses the average within the deviation band.",
    Ru = "Адаптивная скользящая средняя Кауфмана: скорость сглаживания подстраивается под шум рынка - во флэте линия замирает и не реагирует на шум, а с приходом реального тренда быстро ускоряется к быстрой средней. Пересечение линии отмечает изменение, пережившее шумовой фильтр. Алерт срабатывает, когда свеча пересекает среднюю в пределах полосы допуска.",
    Es = "La media móvil adaptativa de Kaufman: la velocidad de suavizado se adapta al ruido del mercado - en un mercado lateral la línea se congela e ignora el ruido, y cuando comienza una tendencia real se acelera hacia la media rápida. Un cruce de la línea marca un cambio que superó el filtro de ruido. La alerta se activa cuando una vela cruza la media dentro de la banda de tolerancia.",
    It = "Media mobile adattiva di Kaufman: la velocità di smoothing si adatta al rumore del mercato - in un mercato laterale e tranquillo la linea si congela e ignora il rumore, e quando inizia un trend reale si accelera rapidamente verso la media veloce. Un incrocio della linea segna un cambiamento che ha superato il filtro del rumore. L'avviso scatta quando una candela incrocia la media entro la banda di tolleranza.",
    De = "Adaptiver gleitender Durchschnitt nach Kaufman: die Geschwindigkeit der Glättung passt sich dem Rauschen des Marktes an - in einem ruhigen Seitwärtsmarkt erstarrt die Linie und ignoriert das Rauschen, und sobald ein echter Trend beginnt, beschleunigt sie sich schnell in Richtung des schnellen Durchschnitts. Eine Kreuzung der Linie markiert eine Änderung, die den Rauschfilter überstanden hat. Der Alert wird ausgelöst, wenn eine Kerze den Durchschnitt innerhalb der Toleranzbandbreite kreuzt.",
    Fr = "Moyenne mobile adaptive de Kaufman : la vitesse de lissage s'adapte au bruit du marché - dans un marché calme et sans tendance, la ligne se fige et ignore le bruit, puis dès qu'une vraie tendance démarre elle accélère rapidement vers la moyenne rapide. Un croisement de la ligne signale un changement qui a survécu au filtre de bruit. L'alerte se déclenche lorsqu'une bougie croise la moyenne à l'intérieur de la bande de tolérance.",
    PtBr = "Média móvel adaptativa de Kaufman: a velocidade de suavização se adapta ao ruído do mercado - num mercado lateral e tranquilo a linha congela e ignora o ruído, e quando uma tendência real começa ela acelera rapidamente em direção à média rápida. Um cruzamento da linha marca uma mudança que sobreviveu ao filtro de ruído. O alerta é acionado quando uma vela cruza a média dentro da banda de tolerância.",
    Uk = "Адаптивна ковзна середня Кауфмана: швидкість згладжування підлаштовується під ринковий шум - у спокійному боковику лінія завмирає і ігнорує шум, а з приходом справжнього тренду швидко прискорюється до швидкої середньої. Перетин лінії позначає зміну, що пережила шумовий фільтр. Алерт спрацьовує, коли свічка перетинає середню в межах смуги допуску.",
    Be = "Адаптыўная слізгальная сярэдняя Каўфмана: хуткасць згладжвання падладжваецца пад рынкавы шум - у спакойным бакавіку лінія замірае і ігнаруе шум, а з прыходам сапраўднага трэнду хутка паскараецца да хуткай сярэдняй. Перасячэнне лініі адзначае змену, якая перажыла шумавы фільтр. Апавяшчэнне спрацоўвае, калі свечка перасякае сярэднюю ў межах палы допуску.")]
public class KamaIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
      En = "Efficiency ratio period: the window over which the line measures how noisy the market is. Standard is 20.",
      Ru = "Период коэффициента эффективности: окно, по которому линия измеряет, насколько шумен рынок. Стандарт - 20.",
      Es = "Periodo del ratio de eficiencia: la ventana con la que la línea mide cuánto ruido hay en el mercado. El estándar es 20.",
      It = "Periodo del rapporto di efficienza: la finestra su cui la linea misura quanto è rumoroso il mercato. Lo standard è 20.",
      De = "Periode des Effizienzverhältnisses: das Fenster, über das die Linie misst, wie stark der Markt verrauscht ist. Standard ist 20.",
      Fr = "Période du ratio d'efficacité : la fenêtre sur laquelle la ligne mesure le niveau de bruit du marché. Le standard est 20.",
      PtBr = "Período do índice de eficiência: a janela em que a linha mede o quanto o mercado está ruidoso. O padrão é 20.",
      Uk = "Період коефіцієнта ефективності: вікно, за яким лінія вимірює, наскільки шумним є ринок. Стандарт - 20.",
      Be = "Перыяд каэфіцыента эфектыўнасці: акно, па якім лінія вымярае, наколькі шумным з'яўляецца рынак. Стандарт - 20.")]
  public int ErPeriods { get; set; } = 20;

  [ParameterHint(
      En = "Fast average period the line approaches in a strong trend. Standard is 2.",
      Ru = "Период быстрой средней, к которой подходит линия в сильном тренде. Стандарт - 2.",
      Es = "Periodo de la media rápida hacia la que se acerca la línea en una tendencia fuerte. El estándar es 2.",
      It = "Periodo della media veloce a cui la linea si avvicina in un trend forte. Lo standard è 2.",
      De = "Periode des schnellen Durchschnitts, dem sich die Linie in einem starken Trend annähert. Standard ist 2.",
      Fr = "Période de la moyenne rapide que la ligne approche dans une tendance forte. Le standard est 2.",
      PtBr = "Período da média rápida à qual a linha se aproxima em uma tendência forte. O padrão é 2.",
      Uk = "Період швидкої середньої, до якої лінія наближається в сильному тренді. Стандарт - 2.",
      Be = "Перыяд хуткай сярэдняй, да якой лінія набліжаецца ў моцным трэндзе. Стандарт - 2.")]
  public int FastPeriods { get; set; } = 2;

  [ParameterHint(
      En = "Slow average period the line sticks to in a noisy, sideways market. Standard is 30.",
      Ru = "Период медленной средней, которой линия следует в шумном боковом рынке. Стандарт - 30.",
      Es = "Periodo de la media lenta al que se ajusta la línea en un mercado lateral y ruidoso. El estándar es 30.",
      It = "Periodo della media lenta a cui la linea si aggancia in un mercato rumoroso e laterale. Lo standard è 30.",
      De = "Periode des langsamen Durchschnitts, an dem sich die Linie in einem verrauschten Seitwärtsmarkt festhält. Standard ist 30.",
      Fr = "Période de la moyenne lente à laquelle la ligne s'accroche dans un marché bruyant et sans tendance. Le standard est 30.",
      PtBr = "Período da média lenta à qual a linha se ajusta em um mercado ruidoso e lateral. O padrão é 30.",
      Uk = "Період повільної середньої, якої лінія тримається в шумному боковику. Стандарт - 30.",
      Be = "Перыяд павольнай сярэдняй, якой лінія трымаецца ў шумным бакавіку. Стандарт - 30.")]
  public int SlowPeriods { get; set; } = 30;

  [ParameterHint(
      En = "Which price of the candle (open, high, low, close) is used in the indicator calculation.",
      Ru = "Какая цена свечи (открытие, максимум, минимум, закрытие) используется в расчёте индикатора.",
      Es = "Qué precio de la vela (apertura, máximo, mínimo, cierre) se usa en el cálculo del indicador.",
      It = "Quale prezzo della candela (apertura, massimo, minimo, chiusura) viene usato nel calcolo dell'indicatore.",
      De = "Welcher Kerzenkurs (Eröffnung, Hoch, Tief, Schluss) für die Indikatorberechnung verwendet wird.",
      Fr = "Quel prix de la bougie (ouverture, plus haut, plus bas, clôture) est utilisé dans le calcul de l'indicateur.",
      PtBr = "Qual preço da vela (abertura, máxima, mínima, fechamento) é usado no cálculo do indicador.",
      Uk = "Яка ціна свічки (відкриття, максимум, мінімум, закриття) використовується в розрахунку індикатора.",
      Be = "Якая цэна свечкі (адкрыццё, максімум, мінімум, закрыццё) выкарыстоўваецца ў разліку індыкатара.")]
  public CandlePart CandlePart { get; set; } = CandlePart.Close;

  [ParameterHint(
      En = "Tolerance for detecting a price cross of the average, as a fraction of its value (0.0005 = 0.05%). The alert fires when the price crosses the average within this band.",
      Ru = "Допуск при определении пересечения цены средней, в доле от её значения (0.0005 = 0.05%). Алерт срабатывает, когда цена пересекает среднюю в пределах этой полосы.",
      Es = "Tolerancia para detectar el cruce del precio con la media, como fracción de su valor (0.0005 = 0.05%). La alerta se activa cuando el precio cruza la media dentro de esta banda.",
      It = "Tolleranza per rilevare l'incrocio del prezzo con la media, come frazione del suo valore (0.0005 = 0.05%). L'avviso scatta quando il prezzo incrocia la media entro questa banda.",
      De = "Toleranz für die Erkennung der Kreuzung des Kurses mit dem Durchschnitt, als Bruchteil seines Werts (0.0005 = 0.05%). Der Alert wird ausgelöst, wenn der Kurs den Durchschnitt innerhalb dieser Bandbreite kreuzt.",
      Fr = "Tolérance pour détecter le croisement du prix avec la moyenne, en fraction de sa valeur (0.0005 = 0.05%). L'alerte se déclenche lorsque le prix croise la moyenne à l'intérieur de cette bande.",
      PtBr = "Tolerância para detectar o cruzamento do preço com a média, como fração do seu valor (0.0005 = 0.05%). O alerta é acionado quando o preço cruza a média dentro dessa banda.",
      Uk = "Допуск для визначення перетину ціною середньої, у частці від її значення (0.0005 = 0.05%). Алерт спрацьовує, коли ціна перетинає середню в межах цієї смуги.",
      Be = "Допуск для вызначэння перасячэння цаной сярэдняй, у долі ад яе значэння (0.0005 = 0.05%). Апавяшчэнне спрацоўвае, калі цэна перасякае сярэднюю ў межах гэтай паласы.")]
  public double Deviation { get; set; } = 0.0005F;

  public int GetCandleCount() => (ErPeriods + SlowPeriods) * 2;
}
