using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "Price change in percent between the latest candle's close and the close N candles back. The alert fires when the change crosses the threshold (entering the zone), not on every candle that stays beyond it. Direction lets you watch rallies, drops or either side.",
    Ru = "Изменение цены в процентах между закрытием последней свечи и закрытием свечи N свечей назад. Алерт срабатывает при переходе через порог (входе в зону), а не на каждой свече, что осталась за порогом. Направление позволяет следить за ростом, падением или обеими сторонами.",
    Es = "Cambio de precio en porcentaje entre el cierre de la última vela y el cierre de hace N velas. La alerta se activa al cruzar el umbral (entrada en la zona), no en cada vela que permanece más allá de él. La dirección permite vigilar subidas, caídas o ambos lados.",
    It = "Variazione percentuale del prezzo tra la chiusura dell'ultima candela e la chiusura di N candele fa. L'avviso scatta quando la variazione incrocia la soglia (ingresso nella zona), non su ogni candela che resta oltre di essa. La direzione permette di monitorare rialzi, cali o entrambi i lati.",
    De = "Prozentuale Preisänderung zwischen dem Schlusskurs der letzten Kerze und dem Schlusskurs vor N Kerzen. Der Alert wird ausgelöst, wenn die Änderung die Schwelle überschreitet (Eintritt in die Zone), nicht bei jeder Kerze, die darüber bleibt. Die Richtung ermöglicht es, Anstiege, Fälle oder beide Seiten zu beobachten.",
    Fr = "Variation de prix en pourcentage entre la clôture de la dernière bougie et la clôture d'il y a N bougies. L'alerte se déclenche quand la variation franchit le seuil (entrée dans la zone), pas sur chaque bougie qui reste au-delà. La direction permet de surveiller les hausses, les baisses ou les deux.",
    PtBr = "Variação percentual do preço entre o fechamento da última vela e o fechamento de N velas atrás. O alerta é acionado quando a variação cruza o limiar (entrada na zona), não em cada vela que permanece além dele. A direção permite acompanhar altas, quedas ou ambos os lados.",
    Uk = "Зміна ціни у відсотках між закриттям останньої свічки та закриттям свічки N свічок тому. Алерт спрацьовує, коли зміна перетинає поріг (вхід у зону), а не на кожній свічці, що залишається за порогом. Напрям дозволяє стежити за зростанням, падінням або обома боками.",
    Be = "Змена цаны ў адсотках паміж закрыццём апошняй свічкі і закрыццём свічкі N свечак таму. Апавяшчэнне спрацоўвае, калі змена перасякае парог (уваход у зону), а не на кожнай свечцы, якая застаецца па-за парогам. Напрамак дазваляе сачыць за ростам, падзеннем або абодвума бакамі.")]
public class PriceChangeIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
      En = "Lookback in candles: the change is measured from the close that many candles before the latest one.",
      Ru = "Глубина в свечах: изменение считается от закрытия, что было столько свечей назад.",
      Es = "Retroceso en velas: el cambio se mide desde el cierre de hace tantas velas.",
      It = "Numero di candele di lookback: la variazione viene misurata dalla chiusura di quel numero di candele prima dell'ultima.",
      De = "Rückblick in Kerzen: Die Änderung wird ab dem Schlusskurs vor so vielen Kerzen gemessen.",
      Fr = "Recul en bougies : la variation est mesurée à partir de la clôture située autant de bougies avant la dernière.",
      PtBr = "Recuo em velas: a variação é medida a partir do fechamento de tantas velas antes da última.",
      Uk = "Глибина в свічках: зміна вимірюється від закриття, що було стільки свічок тому.",
      Be = "Глыбіня ў свечках: змена вымяраецца ад закрыцця, якое было столькі свечак таму.")]
  public int Periods { get; set; } = 10;

  [ParameterHint(
      En = "Threshold in percent. The alert fires when the change crosses this magnitude.",
      Ru = "Порог в процентах. Алерт срабатывает при переходе изменения через эту величину.",
      Es = "Umbral en porcentaje. La alerta se activa cuando el cambio cruza este valor.",
      It = "Soglia in percentuale. L'avviso scatta quando la variazione supera questa entità.",
      De = "Schwelle in Prozent. Der Alert wird ausgelöst, wenn die Änderung diese Größe überschreitet.",
      Fr = "Seuil en pourcentage. L'alerte se déclenche lorsque la variation dépasse cette amplitude.",
      PtBr = "Limiar em porcentagem. O alerta é acionado quando a variação ultrapassa essa magnitude.",
      Uk = "Поріг у відсотках. Алерт спрацьовує, коли зміна перевищує цю величину.",
      Be = "Парог у адсотках. Апавяшчэнне спрацоўвае, калі змена перавышае гэтую велічыню.")]
  public double Percent { get; set; } = 1.0;

  [ParameterHint(
      En = "Which side of the move fires the alert: Up - growth by at least Percent, Down - drop by at least Percent, Both - the magnitude of the change in either direction.",
      Ru = "Какая сторона движения даёт алерт: «Рост» - рост не меньше «Процент», «Падение» - падение не меньше «Процент», «Оба» - модуль изменения в любую сторону.",
      Es = "Qué lado del movimiento activa la alerta: «Subida» - subida de al menos «Porcentaje», «Caída» - caída de al menos «Porcentaje», «Ambos» - magnitud del cambio en cualquier dirección.",
      It = "Quale lato del movimento attiva l'avviso: Up - rialzo di almeno Percent, Down - calo di almeno Percent, Both - l'entità della variazione in una qualsiasi direzione.",
      De = "Welche Seite der Bewegung den Alert auslöst: Up - Anstieg um mindestens Percent, Down - Rückgang um mindestens Percent, Both - der Betrag der Änderung in beide Richtungen.",
      Fr = "Quel côté du mouvement déclenche l'alerte : Up - hausse d'au moins Percent, Down - baisse d'au moins Percent, Both - l'amplitude de la variation dans n'importe quelle direction.",
      PtBr = "Qual lado do movimento aciona o alerta: Up - alta de pelo menos Percent, Down - queda de pelo menos Percent, Both - a magnitude da variação em qualquer direção.",
      Uk = "Який бік руху дає алерт: Up - зростання щонайменше на Percent, Down - падіння щонайменше на Percent, Both - модуль зміни в будь-який бік.",
      Be = "Які бок руху дае апавяшчэнне: Up - рост прынамсі на Percent, Down - падзенне прынамсі на Percent, Both - модуль змены ў любы бок.")]
  public PriceChangeDirection Direction { get; set; } = PriceChangeDirection.Both;

  public int GetCandleCount() => Periods + 1;
}
