using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "The price gap between the previous candle's close and the current candle's open. Gaps form when new information arrives outside of trading hours (news, earnings, macro data). The alert fires when the gap is at least the given percent of price, at most once per day.",
    Ru = "Разрыв цены между закрытием предыдущей свечи и открытием текущей. Гэпы возникают, когда вне торгов поступает новая информация (новости, отчётность, макроданные). Алерт срабатывает, когда гэп не меньше заданного процента от цены, не чаще одного раза в день.",
    Es = "El salto de precio entre el cierre de la vela anterior y la apertura de la actual. Los gaps se forman cuando llega nueva información fuera del horario de negociación (noticias, resultados, datos macro). La alerta se activa cuando el gap es al menos el porcentaje indicado del precio, como máximo una vez al día.",
    It = "Il gap di prezzo tra la chiusura della candela precedente e l'apertura di quella attuale. I gap si formano quando arrivano nuove informazioni fuori dall'orario di negoziazione (notizie, risultati, dati macro). L'avviso scatta quando il gap è almeno la percentuale indicata del prezzo, al massimo una volta al giorno.",
    De = "Die Kurslücke zwischen dem Schlusskurs der vorherigen und dem Eröffnungskurs der aktuellen Kerze. Gaps entstehen, wenn außerhalb der Handelszeiten neue Informationen eintreffen (Nachrichten, Ergebnisse, Makrodaten). Der Alert wird ausgelöst, wenn das Gap mindestens dem angegebenen Prozentsatz des Kurses entspricht, höchstens einmal pro Tag.",
    Fr = "Le gap de prix entre la clôture de la bougie précédente et l'ouverture de la bougie actuelle. Les gaps se forment lorsque de nouvelles informations arrivent en dehors des heures de négociation (actualités, résultats, données macro). L'alerte se déclenche lorsque le gap représente au moins le pourcentage indiqué du prix, au plus une fois par jour.",
    PtBr = "O gap de preço entre o fechamento da vela anterior e a abertura da vela atual. Os gaps se formam quando chegam novas informações fora do horário de negociação (notícias, resultados, dados macro). O alerta é acionado quando o gap é de pelo menos o percentual indicado do preço, no máximo uma vez por dia.",
    Uk = "Ціновий геп між закриттям попередньої свічки та відкриттям поточної. Гепи утворюються, коли поза торгами з'являється нова інформація (новини, звітність, макродані). Алерт спрацьовує, коли геп не менший за заданий відсоток ціни, не частіше ніж раз на день.",
    Be = "Цянавы гэп паміж закрыццём папярэдняй свечкі і адкрыццём бягучай. Гэпы ўтвараюцца, калі па-за гандлёвымі гадзінамі паступае новая інфармацыя (навіны, справаздачнасць, макрададзеныя). Апавяшчэнне спрацоўвае, калі гэп не меншы за зададзаны працэнт цэны, не часцей за раз на дзень.")]
public class GapIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
      En = "Minimum gap size in percent of price. The alert fires when the gap between the previous close and the current open is at least this size.",
      Ru = "Минимальный размер гэпа в процентах от цены. Алерт срабатывает, когда разрыв между закрытием предыдущей свечи и открытием текущей не меньше этого значения.",
      Es = "Tamaño mínimo del gap en porcentaje del precio. La alerta se activa cuando el salto entre el cierre anterior y la apertura actual es al menos de este tamaño.",
      It = "Dimensione minima del gap in percentuale del prezzo. L'avviso scatta quando il gap tra la chiusura precedente e l'apertura attuale è almeno di questa dimensione.",
      De = "Minimale Gap-Größe in Prozent des Kurses. Der Alert wird ausgelöst, wenn das Gap zwischen dem vorherigen Schlusskurs und der aktuellen Eröffnung mindestens diese Größe hat.",
      Fr = "Taille minimale du gap en pourcentage du prix. L'alerte se déclenche lorsque le gap entre la clôture précédente et l'ouverture actuelle atteint au moins cette taille.",
      PtBr = "Tamanho mínimo do gap em porcentagem do preço. O alerta é acionado quando o gap entre o fechamento anterior e a abertura atual é pelo menos desse tamanho.",
      Uk = "Мінімальний розмір гепу у відсотках від ціни. Алерт спрацьовує, коли геп між попереднім закриттям і поточним відкриттям не менший за це значення.",
      Be = "Мінімальны памер гэпа ў працэнтах ад цэны. Апавяшчэнне спрацоўвае, калі гэп паміж папярэднім закрыццём і бягучым адкрыццём не меншы за гэтае значэнне.")]
  public double Percent { get; set; }

  [ParameterHint(
      En = "Which side of the gap fires the alert: Up - gaps up only (open above the previous close), Down - gaps down only, Both - gaps in either direction.",
      Ru = "Какая сторона гэпа даёт алерт: «Рост» - только гэпы вверх (открытие выше предыдущего закрытия), «Падение» - только гэпы вниз, «Оба» - гэпы в любую сторону.",
      Es = "Qué lado del gap activa la alerta: «Subida» - solo gaps al alza (apertura por encima del cierre anterior), «Caída» - solo gaps a la baja, «Ambos» - gaps en cualquier dirección.",
      It = "Quale lato del gap attiva l'avviso: Up - solo gap rialzisti (apertura sopra la chiusura precedente), Down - solo gap ribassisti, Both - gap in entrambe le direzioni.",
      De = "Welche Seite des Gaps den Alert auslöst: Up - nur Aufwärts-Gaps (Eröffnung über dem Schlusskurs der vorherigen Kerze), Down - nur Abwärts-Gaps, Both - Gaps in beide Richtungen.",
      Fr = "Quel côté du gap déclenche l'alerte : Up - gaps haussiers uniquement (ouverture au-dessus de la clôture précédente), Down - gaps baissiers uniquement, Both - gaps dans les deux sens.",
      PtBr = "Qual lado do gap aciona o alerta: Up - só gaps de alta (abertura acima do fechamento anterior), Down - só gaps de queda, Both - gaps em qualquer direção.",
      Uk = "Який бік гепу дає алерт: Up - лише гепи вгору (відкриття вище за попереднє закриття), Down - лише гепи вниз, Both - гепи в будь-який бік.",
      Be = "Які бок гэпа дае апавяшчэнне: Up - толькі гэпы ўгару (адкрыццё вышэй за папярэдняе закрыццё), Down - толькі гэпы ўніз, Both - гэпы ў любы бок.")]
  public AlertDirection Direction { get; set; } = AlertDirection.Both;

  public int GetCandleCount() => 2;
}
