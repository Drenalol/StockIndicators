using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "The cumulative balance of volume: volume is added on up candles and subtracted on down ones. A divergence between OBV and price often precedes a reversal - volume confirms or refutes the trend. The alert fires when OBV crosses its smoothing EMA.",
    Ru = "Накопленный баланс объёмов: на свечах роста объём прибавляется, на падении - вычитается. Дивергенция OBV и цены часто предвещает разворот - объём подтверждает или опровергает тренд. Алерт срабатывает, когда OBV пересекает свою сглаживающую EMA.",
    Es = "El balance acumulado de volumen: el volumen se suma en las velas alcistas y se resta en las bajistas. Una divergencia entre el OBV y el precio suele anticipar un reversal - el volumen confirma o refuta la tendencia. La alerta se activa cuando el OBV cruza su EMA de suavizado.",
    It = "Il saldo cumulato del volume: il volume viene somato sulle candele rialziste e sottratto su quelle ribassiste. Una divergenza tra OBV e prezzo spesso precede un'inversione - il volume conferma o smentisce il trend. L'avviso scatta quando l'OBV incrocia la sua EMA di livellamento.",
    De = "Die kumulierte Volumenbilanz: bei Aufwärtskerzen wird das Volumen addiert, bei Abwärtskerzen subtrahiert. Eine Divergenz zwischen OBV und Kurs kündigt oft eine Umkehr an - das Volumen bestätigt oder widerlegt den Trend. Der Alert wird ausgelöst, wenn der OBV seine geglättete EMA kreuzt.",
    Fr = "Le solde cumulé des volumes : le volume est ajouté sur les bougies haussières et soustrait sur les bougies baissières. Une divergence entre l'OBV et le prix précède souvent un retournement : le volume confirme ou infirme la tendance. L'alerte se déclenche lorsque l'OBV croise son EMA de lissage.",
    PtBr = "O saldo acumulado do volume: o volume é somado nas velas de alta e subtraído nas velas de queda. Uma divergência entre o OBV e o preço frequentemente antecede uma reversão - o volume confirma ou refuta a tendência. O alerta é acionado quando o OBV cruza a sua EMA de suavização.",
    Uk = "Накопичуваний баланс обсягів: на свічках зростання обсяг додається, на падінні - віднімається. Дивергенція OBV і ціни часто передує розвороту - обсяг підтверджує чи спростовує тренд. Алерт спрацьовує, коли OBV перетинає свою згладжуючу EMA.",
    Be = "Назапашаны баланс аб'ёмаў: на свечках росту аб'ём дадаецца, на падзенні - аднімаецца. Дывергенцыя OBV і цаны часта папярэджвае пра разварот - аб'ём пацвярджае ці абвяргае трэнд. Апавяшчэнне спрацоўвае, калі OBV перасякае сваю згладжвальную EMA.")]
public class ObvIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
      En = "Period of the EMA applied to OBV for trend smoothing. The alert fires when OBV crosses this EMA.",
      Ru = "Период EMA, применяемой к OBV для сглаживания тренда. Алерт срабатывает при пересечении OBV этой EMA.",
      Es = "Periodo de la EMA aplicada al OBV para suavizar la tendencia. La alerta se activa cuando el OBV cruza esta EMA.",
      It = "Periodo dell'EMA applicata all'OBV per livellare il trend. L'avviso scatta quando l'OBV incrocia questa EMA.",
      De = "Zeitraum der EMA, die zur Glättung des OBV-Trends angewendet wird. Der Alert wird ausgelöst, wenn der OBV diese EMA kreuzt.",
      Fr = "Période de l'EMA appliquée à l'OBV pour lisser la tendance. L'alerte se déclenche lorsque l'OBV croise cette EMA.",
      PtBr = "Período da EMA aplicada ao OBV para suavizar a tendência. O alerta é acionado quando o OBV cruza essa EMA.",
      Uk = "Період EMA, що застосовується до OBV для згладжування тренду. Алерт спрацьовує при перетині OBV цієї EMA.",
      Be = "Перыяд EMA, якая прымяняецца да OBV для згладжвання трэнду. Апавяшчэнне спрацоўвае пры перасячэнні OBV гэтай EMA.")]
  public int EmaPeriod { get; set; } = 20;

  public int GetCandleCount() => EmaPeriod * 2;
}
