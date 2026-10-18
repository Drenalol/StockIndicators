using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "A momentum oscillator on the -100 to 100 scale: a doubly smoothed rate of change of price, with its own signal line. It stays closer to zero than raw momentum and turns when momentum decays. The alert fires when the TSI line crosses its signal line.",
    Ru = "Импульсный осциллятор в шкале от -100 до 100: двойная экспоненциальная сглаженная скорость изменения цены с собственной линией сигнала. Держится ближе к нулю, чем грубый моментум, и разворачивается при затухании импульса. Алерт срабатывает при пересечении линии TSI своей сигнальной линией.",
    Es = "Un oscilador de momentum en la escala de -100 a 100: una tasa de cambio de precio doblemente suavizada con su propia línea de señal. Se mantiene más cerca de cero que el momentum bruto y gira cuando el impulso decae. La alerta se activa cuando la línea TSI cruza su línea de señal.",
    It = "Un oscillatore di momentum su scala da -100 a 100: un tasso di variazione del prezzo con doppio smoothing esponenziale, dotato di una propria linea del segnale. Resta più vicino allo zero rispetto al momentum grezzo e svolta quando il momentum si esaurisce. L'avviso scatta quando la linea TSI incrocia la sua linea del segnale.",
    De = "Ein Momentum-Oszillator auf der Skala von -100 bis 100: eine doppelt geglättete Änderungsrate des Kurses mit eigener Signallinie. Er bleibt näher bei Null als das rohe Momentum und dreht, wenn das Momentum abklingt. Der Alert wird ausgelöst, wenn die TSI-Linie ihre Signallinie kreuzt.",
    Fr = "Un oscillateur de momentum sur l'échelle de -100 à 100 : un taux de variation du prix doublement lissé, avec sa propre ligne de signal. Il reste plus proche de zéro que le momentum brut et se retourne quand le momentum s'essouffle. L'alerte se déclenche quand la ligne TSI croise sa ligne de signal.",
    PtBr = "Um oscilador de momentum na escala de -100 a 100: uma taxa de variação de preço duplamente suavizada, com sua própria linha de sinal. Ele fica mais perto de zero que o momentum bruto e vira quando o momentum esmorece. O alerta é acionado quando a linha TSI cruza sua linha de sinal.",
    Uk = "Імпульсний осциллятор у шкалі від -100 до 100: подвійно згладжена швидкість зміни ціни з власною сигнальною лінією. Тримається ближче до нуля, ніж чистий моментум, і розвертається при згасанні імпульсу. Алерт спрацьовує, коли лінія TSI перетинає свою сигнальну лінію.",
    Be = "Імпульсны асцылятар у шкале ад -100 да 100: двойчы згладжаная хуткасць змены цаны з уласнай сігнальнай лініяй. Трымаецца бліжэй да нуля, чым чысты маментум, і разварочваецца пры згасанні імпульсу. Апавяшчэнне спрацоўвае, калі лінія TSI перасякае сваю сігнальную лінію.")]
public class TsiIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
      Be = "Таймфрэйм свечак, на якім разлічваецца індыкатар. Апавяшчэнне правяраецца пры закрыцці кожнай свечкі гэтага перыяду.")]
  public TimeFrame TimeFrame { get; set; }

  [ParameterHint(
      En = "Long period of the double exponential smoothing of price changes. Standard is 25.",
      Ru = "Длинный период двойного экспоненциального сглаживания изменений цены. Стандарт - 25.",
      Es = "Periodo largo del doble suavizado exponencial de los cambios de precio. El estándar es 25.",
      It = "Periodo lungo del doppio smoothing esponenziale delle variazioni di prezzo. Lo standard è 25.",
      De = "Lange Periode der doppelten exponentiellen Glättung der Kursänderungen. Standard ist 25.",
      Fr = "Période longue du double lissage exponentiel des variations de prix. La valeur standard est 25.",
      PtBr = "Período longo da dupla suavização exponencial das variações de preço. O padrão é 25.",
      Uk = "Довгий період подвійного експоненційного згладжування змін ціни. Стандарт - 25.",
      Be = "Доўгі перыяд падвойнага экспанэнцыяльнага згладжвання змен цаны. Стандарт - 25.")]
  public int LongPeriods { get; set; } = 25;

  [ParameterHint(
      En = "Period of the TSI signal line. The alert fires on a crossover of the TSI line with this signal line. Standard is 13.",
      Ru = "Период сигнальной линии TSI. Алерт срабатывает при пересечении линии TSI с этой линией сигнала. Стандарт - 13.",
      Es = "Periodo de la línea de señal del TSI. La alerta se activa cuando la línea TSI cruza esta línea de señal. El estándar es 13.",
      It = "Periodo della linea del segnale del TSI. L'avviso scatta quando la linea TSI incrocia questa linea del segnale. Lo standard è 13.",
      De = "Periode der TSI-Signallinie. Der Alert wird bei einer Kreuzung der TSI-Linie mit dieser Signallinie ausgelöst. Standard ist 13.",
      Fr = "Période de la ligne de signal du TSI. L'alerte se déclenche quand la ligne TSI croise cette ligne de signal. La valeur standard est 13.",
      PtBr = "Período da linha de sinal do TSI. O alerta é acionado quando a linha TSI cruza essa linha de sinal. O padrão é 13.",
      Uk = "Період сигнальної лінії TSI. Алерт спрацьовує при перетині лінії TSI цією сигнальною лінією. Стандарт - 13.",
      Be = "Перыяд сігнальнай лініі TSI. Апавяшчэнне спрацоўвае пры перасячэнні лініі TSI гэтай сігнальнай лініяй. Стандарт - 13.")]
  public int SignalPeriods { get; set; } = 13;
  public int GetCandleCount() => (LongPeriods + SignalPeriods) * 2;
}
