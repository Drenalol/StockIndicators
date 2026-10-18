using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "Parabolic SAR: a dot line that trails price with an accelerating step and flips sides when the trend is broken - below price in an uptrend, above it in a downtrend. The longer the trend, the closer the dots approach the price. The alert fires when the dots switch sides, i.e. the trend reverses.",
    Ru = "Параболический SAR: линия точек, которая следует за ценой с ускоряющимся шагом и меняет сторону при пробое тренда - под ценой в восходящем тренде, над ней в нисходящем. Чем дольше тренд, тем ближе точки подходят к цене. Алерт срабатывает при смене стороны точек, то есть при развороте тренда.",
    Es = "Parabolic SAR: una línea de puntos que sigue el precio con un paso acelerado y cambia de lado cuando la tendencia se rompe: por debajo del precio en una tendencia alcista, por encima en una bajista. Cuanto más dura la tendencia, más se acercan los puntos al precio. La alerta se activa cuando los puntos cambian de lado, es decir, la tendencia se invierte.",
    It = "Parabolic SAR: una linea di punti che insegue il prezzo con un passo accelerato e cambia lato quando il trend viene violato - sotto il prezzo in un trend rialzista, sopra in un trend ribassista. Più il trend dura a lungo, più i punti si avvicinano al prezzo. L'avviso scatta quando i punti cambiano lato, cioè quando il trend inverte.",
    De = "Parabolic SAR: eine Punktelinie, die dem Kurs mit beschleunigendem Schritt folgt und die Seite wechselt, wenn der Trend gebrochen wird - unter dem Kurs in einem Aufwärtstrend, darüber in einem Abwärtstrend. Je länger der Trend dauert, desto näher kommen die Punkte dem Kurs. Der Alert wird ausgelöst, wenn die Punkte die Seite wechseln, d. h. wenn der Trend dreht.",
    Fr = "Parabolic SAR : une ligne de points qui suit le prix avec un pas accéléré et change de côté quand la tendance est cassée - sous le prix dans une tendance haussière, au-dessus dans une tendance baissière. Plus la tendance dure, plus les points se rapprochent du prix. L'alerte se déclenche quand les points changent de côté, c'est-à-dire quand la tendance s'inverse.",
    PtBr = "Parabolic SAR: uma linha de pontos que persegue o preço com um passo acelerado e muda de lado quando a tendência é rompida - abaixo do preço em tendência de alta, acima em tendência de queda. Quanto mais a tendência dura, mais os pontos se aproximam do preço. O alerta é acionado quando os pontos trocam de lado, ou seja, a tendência reverte.",
    Uk = "Параболічний SAR: лінія точок, яка супроводжує ціну з прискорюваним кроком і змінює бік при пробії тренду - під ціною у висхідному тренді, над нею у спадному. Чим довший тренд, тим ближче точки підходять до ціни. Алерт спрацьовує, коли точки змінюють бік, тобто коли тренд розвертається.",
    Be = "Парабалічны SAR: лінія кропак, якая ідзе за цаной з паскаральным крокам і мяняе бок пры пробоі трэнду - пад цаной ва ўзыходным трэндзе, над ёй у низыходным. Чым даўжэй доўжыцца трэнд, тым бліжэй кропкі падыходзяць да цаны. Апавяшчэнне спрацоўвае пры змене боку кропкамі, гэта значыць пры развароце трэнду.")]
public class PsarIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
{
  [ParameterHint(
      En = "Candle timeframe the indicator is calculated on. The alert is evaluated on each closed candle of this period.",
      Ru = "Таймфрейм свечей, по которым считается индикатор. Проверка выполняется при закрытии каждой свечи этого периода.",
      Es = "Temporalidad de las velas con la que se calcula el indicador. La alerta se evalúa al cierre de cada vela de este periodo.",
      It = "Timeframe della candela su cui viene calcolato l'indicatore. L'avviso viene valutato alla chiusura di ogni candela di questo periodo.",
      De = "Zeitrahmen der Kerzen, auf dem der Indikator berechnet wird. Der Alert wird beim Schluss jeder Kerze dieses Zeitraums ausgewertet.",
      Fr = "Unité de temps des bougies sur laquelle l'indicateur est calculé. L'alerte est évaluée à la clôture de chaque bougie de cette période.",
      PtBr = "Timeframe das velas no qual o indicador é calculado. O alerta é avaliado no fechamento de cada vela desse período.",
      Uk = "Таймфрейм свічок, на якому розраховується індикатор. Алерт перевіряється на кожній закритій свічці цього періоду.",
      Be = "Таймфрэйм свічак, на якім разлічваецца індыкатар. Апавяшчэнне правяраецца на кожнай зачыненай свічцы гэтага перыяду.")]
  public TimeFrame TimeFrame { get; set; }

  [ParameterHint(
      En = "Acceleration step: how fast the SAR line approaches price while the trend lasts. Standard is 0.02.",
      Ru = "Шаг ускорения: как быстро линия SAR подходит к цене по мере длятся тренда. Стандарт - 0,02.",
      Es = "Paso de aceleración: con qué rapidez la línea SAR se acerca al precio mientras dura la tendencia. El estándar es 0,02.",
      It = "Passo di accelerazione: con quale velocità la linea SAR si avvicina al prezzo mentre il trend prosegue. Lo standard è 0.02.",
      De = "Beschleunigungsschritt: wie schnell die SAR-Linie dem Kurs im laufenden Trend folgt. Standard ist 0.02.",
      Fr = "Pas d'accélération : la vitesse à laquelle la ligne SAR se rapproche du prix tant que la tendance dure. La valeur standard est 0.02.",
      PtBr = "Passo de aceleração: com que velocidade a linha SAR se aproxima do preço enquanto a tendência dura. O padrão é 0.02.",
      Uk = "Крок прискорення: як швидко лінія SAR наближається до ціни, поки триває тренд. Стандарт - 0.02.",
      Be = "Крок паскарэння: як хутка лінія SAR набліжаецца да цаны, пакуль доўжыцца трэнд. Стандарт - 0.02.")]
  public double AccelerationStep { get; set; } = 0.02;

  [ParameterHint(
      En = "Maximum acceleration factor the SAR line can reach. Standard is 0.2.",
      Ru = "Максимальный коэффициент ускорения, которого может достичь линия SAR. Стандарт - 0,2.",
      Es = "Factor de aceleración máximo que puede alcanzar la línea SAR. El estándar es 0,2.",
      It = "Fattore di accelerazione massimo che la linea SAR può raggiungere. Lo standard è 0.2.",
      De = "Maximaler Beschleunigungsfaktor, den die SAR-Linie erreichen kann. Standard ist 0.2.",
      Fr = "Facteur d'accélération maximal que la ligne SAR peut atteindre. La valeur standard est 0.2.",
      PtBr = "Fator de aceleração máximo que a linha SAR pode alcançar. O padrão é 0.2.",
      Uk = "Максимальний коефіцієнт прискорення, якого може досягти лінія SAR. Стандарт - 0.2.",
      Be = "Максімальны каэфіцыент паскарэння, якога можа дасягнуць лінія SAR. Стандарт - 0.2.")]
  public double MaxAccelerationFactor { get; set; } = 0.2;
  public int GetCandleCount() => 50;
}
