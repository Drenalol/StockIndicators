using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "Percent change of the latest candle's close from its own open - the classic \"today is up/down X% since the open\" alert. The alert fires when the move crosses the threshold (entering the zone), not on every candle that stays beyond it; on a daily chart the open is the day's opening price, and a fresh session resets the reference. Direction lets you watch rallies, drops or either side.",
    Ru = "Изменение цены в процентах от открытия последней свечи до её закрытия - каноничное «сегодня упала/выросла на X% от открытия дня». На дневном графике открытием считается цена открытия текущего дня, а с новой сессией отсчёт начинается заново. Алерт срабатывает при переходе через порог (входе в зону), а не на каждой свече, что осталась за порогом. Направление позволяет следить за ростом, падением или обеими сторонами.",
    Es = "Cambio de precio en porcentaje entre la apertura de la última vela y su cierre: la alerta clásica de «hoy sube/baja un X% desde la apertura». En un gráfico diario la apertura es el precio de apertura del día, y una nueva sesión reinicia la referencia. La alerta se activa al cruzar el umbral (entrada en la zona), no en cada vela que permanece más allá de él. La dirección permite vigilar subidas, caídas o ambos lados.",
    It = "Variazione percentuale della chiusura dell'ultima candela rispetto alla sua apertura - il classico avviso «oggi sale/scende del X% dall'apertura». L'avviso scatta quando il movimento incrocia la soglia (ingresso nella zona), non su ogni candela che resta oltre di essa; su un grafico daily l'apertura è il prezzo di apertura del giorno e una nuova sessione reimposta il riferimento. La direzione permette di monitorare rialzi, cali o entrambi i lati.",
    De = "Prozentuale Änderung des Schlusskurses der letzten Kerze gegenüber ihrem Eröffnungskurs - der klassische Alert „heute steigt/fällt der Kurs X % seit der Eröffnung“. Der Alert wird ausgelöst, wenn die Bewegung die Schwelle überschreitet (Eintritt in die Zone), nicht bei jeder Kerze, die darüber bleibt; in einem Tageschart ist die Eröffnung der Eröffnungskurs des Tages, und eine neue Sitzung setzt den Referenzwert zurück. Die Richtung ermöglicht es, Anstiege, Fälle oder beide Seiten zu beobachten.",
    Fr = "Variation en pourcentage de la dernière bougie entre son ouverture et sa clôture - l'alerte classique « aujourd'hui en hausse/baisse de X % depuis l'ouverture ». L'alerte se déclenche quand le mouvement franchit le seuil (entrée dans la zone), pas sur chaque bougie qui reste au-delà ; sur un graphique journalier, l'ouverture est le prix d'ouverture du jour et une nouvelle séance réinitialise la référence. La direction permet de surveiller les hausses, les baisses ou les deux.",
    PtBr = "Variação percentual do fechamento da última vela em relação à sua própria abertura - o alerta clássico «hoje sobe/cai X% desde a abertura». O alerta é acionado quando o movimento cruza o limiar (entrada na zona), não em cada vela que permanece além dele; em um gráfico diário a abertura é o preço de abertura do dia, e uma nova sessão reinicia a referência. A direção permite acompanhar altas, quedas ou ambos os lados.",
    Uk = "Зміна ціни у відсотках від відкриття до закриття останньої свічки - класичний алерт «сьогодні зросла/впала на X% від відкриття». Алерт спрацьовує, коли рух перетинає поріг (вхід у зону), а не на кожній свічці, що залишається за ним; на денному графіку відкриттям є ціна відкриття дня, а нова сесія скидає базу відліку. Напрям дозволяє стежити за зростанням, падінням або обома боками.",
    Be = "Змена цаны ў адсотках ад адкрыцця да закрыцця апошняй свічкі - класічнае апавяшчэнне «сёння вырасла/ўпала на X% ад адкрыцця». Апавяшчэнне спрацоўвае, калі рух перасякае парог (уваход у зону), а не на кожнай свечцы, якая застаецца па-за ім; на дзённным графіку адкрыццём лічыцца цана адкрыцця дня, а з новай сесыяй адлік пачынаецца нанова. Напрамак дазваляе сачыць за ростам, падзеннем або абодвума бакамі.")]
public class OpenChangeIndicatorTemplate : ITimeFrameDependentIndicatorTemplate
{
  [ParameterHint(
      En = "Candle timeframe the indicator is calculated on. Daily candles make it the intraday change from the day's open; smaller timeframes measure the change from the current candle's open.",
      Ru = "Таймфрейм свечей, по которым считается индикатор. На дневных свечах это внутридневное изменение от открытия дня; на меньших таймфреймах - изменение от открытия текущей свечи.",
      Es = "Temporalidad de las velas con la que se calcula el indicador. Con velas diarias es el cambio intradía desde la apertura del día; con temporalidades menores, el cambio desde la apertura de la vela actual.",
      It = "Timeframe della candela su cui viene calcolato l'indicatore. Su candele daily è la variazione intraday dall'apertura del giorno; su timeframe più piccoli misura la variazione dall'apertura della candela corrente.",
      De = "Zeitrahmen der Kerzen, auf dem der Indikator berechnet wird. Bei Tageskerzen ist es die intraday Änderung seit der Eröffnung des Tages; bei kleineren Zeitrahmen die Änderung seit der Eröffnung der aktuellen Kerze.",
      Fr = "Unité de temps des bougies sur laquelle l'indicateur est calculé. Sur des bougies journalières, c'est la variation intrajournalière depuis l'ouverture du jour ; sur des unités de temps plus petites, la variation depuis l'ouverture de la bougie en cours.",
      PtBr = "Timeframe das velas no qual o indicador é calculado. Em velas diárias, é a variação intradiária desde a abertura do dia; em timeframes menores, mede a variação desde a abertura da vela atual.",
      Uk = "Таймфрейм свічок, на якому розраховується індикатор. На денних свічках це внутрішньоденна зміна від відкриття дня; на менших таймфреймах - зміна від відкриття поточної свічки.",
      Be = "Таймфрэйм свічак, на якім разлічваецца індыкатар. На дзённых свечках гэта ўнутрыдзённая змена ад адкрыцця дня; на меншых таймфрэймах - змена ад адкрыцця бягучай свічкі.")]
  public TimeFrame TimeFrame { get; set; } = TimeFrame.Day;

  [ParameterHint(
      En = "Threshold in percent. The alert fires when the change from the open crosses this magnitude.",
      Ru = "Порог в процентах. Алерт срабатывает, когда изменение от открытия пересекает эту величину.",
      Es = "Umbral en porcentaje. La alerta se activa cuando el cambio desde la apertura cruza este valor.",
      It = "Soglia in percentuale. L'avviso scatta quando la variazione dall'apertura supera questo valore.",
      De = "Schwelle in Prozent. Der Alert wird ausgelöst, wenn die Änderung seit der Eröffnung diese Größe überschreitet.",
      Fr = "Seuil en pourcentage. L'alerte se déclenche lorsque la variation depuis l'ouverture dépasse cette amplitude.",
      PtBr = "Limiar em porcentagem. O alerta é acionado quando a variação desde a abertura ultrapassa essa magnitude.",
      Uk = "Поріг у відсотках. Алерт спрацьовує, коли зміна від відкриття перевищує цю величину.",
      Be = "Парог у адсотках. Апавяшчэнне спрацоўвае, калі змена ад адкрыцця перавышае гэтую велічыню.")]
  public double Percent { get; set; } = 2.0;

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
}
