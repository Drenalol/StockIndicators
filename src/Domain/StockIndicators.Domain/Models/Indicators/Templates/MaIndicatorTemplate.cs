using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "The arithmetic mean of the last N candle prices. Smooths out short-term noise and shows the direction of the trend: price above the average is considered bullish, below it - bearish. The alert fires when a candle opens on one side of the average and closes on the other side within the deviation band.",
    Ru = "Среднее арифметическое цен последних N свечей. Сглаживает краткосрочный шум и показывает направление тренда: цена выше средней - бычий сигнал, ниже - медвежий. Алерт срабатывает, когда свеча открывается с одной стороны средней и закрывается с другой в пределах полосы допуска.",
    Es = "La media aritmética de los precios de las últimas N velas. Suaviza el ruido de corto plazo y muestra la dirección de la tendencia: precio sobre la media se considera alcista, bajo ella - bajista. La alerta se activa cuando una vela abre a un lado de la media y cierra al otro dentro de la banda de tolerancia.",
    It = "La media aritmetica dei prezzi delle ultime N candele. Attenua il rumore di breve periodo e mostra la direzione del trend: il prezzo sopra la media è considerato rialzista, sotto la media - ribassista. L'avviso scatta quando una candela apre da un lato della media e chiude dall'altro entro la banda di tolleranza.",
    De = "Das arithmetische Mittel der Kurse der letzten N Kerzen. Glättet kurzfristiges Rauschen und zeigt die Trendrichtung: ein Kurs über dem Durchschnitt gilt als bullisch, darunter - als bärisch. Der Alert wird ausgelöst, wenn eine Kerze auf der einen Seite des Durchschnitts eröffnet und auf der anderen innerhalb der Toleranzbandbreite schließt.",
    Fr = "La moyenne arithmétique des prix des N dernières bougies. Elle lisse le bruit à court terme et indique la direction de la tendance : un prix au-dessus de la moyenne est considéré comme haussier, en dessous - comme baissier. L'alerte se déclenche lorsqu'une bougie ouvre d'un côté de la moyenne et clôture de l'autre à l'intérieur de la bande de tolérance.",
    PtBr = "A média aritmética dos preços das últimas N velas. Suaviza o ruído de curto prazo e mostra a direção da tendência: preço acima da média é considerado altista, abaixo - baixista. O alerta é acionado quando uma vela abre de um lado da média e fecha do outro dentro da banda de tolerância.",
    Uk = "Середнє арифметичне цін останніх N свічок. Згладжує короткостроковий шум і показує напрям тренду: ціна вище середньої вважається бичачою, нижче - ведмежою. Алерт спрацьовує, коли свічка відкривається з одного боку середньої і закривається з іншого в межах смуги допуску.",
    Be = "Сярэдняе арыфметычнае цэн апошніх N свечак. Згладжвае кароткатэрміновы шум і паказвае кірунак трэнду: цэна вышэй за сярэднюю лічыцца бічынай, ніжэй - мядзведжай. Апавяшчэнне спрацоўвае, калі свечка адкрываецца з аднаго боку сярэдняй і закрываецца з другога ў межах палы допуску.")]
public class MaIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
      En = "Number of candles used to calculate the moving average.",
      Ru = "Сколько свечей участвует в расчёте скользящей средней.",
      Es = "Número de velas utilizadas para calcular la media móvil.",
      It = "Numero di candele usate per calcolare la media mobile.",
      De = "Anzahl der Kerzen, mit denen der gleitende Durchschnitt berechnet wird.",
      Fr = "Nombre de bougies utilisées pour calculer la moyenne mobile.",
      PtBr = "Número de velas usadas para calcular a média móvel.",
      Uk = "Скільки свічок бере участь у розрахунку ковзної середньої.",
      Be = "Солькі свечак удзельнічае ў разліку слізгальнай сярэдняй.")]
  public int Periods { get; set; }

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
  public virtual int GetCandleCount() => Periods;
}