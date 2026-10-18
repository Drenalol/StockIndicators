using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "A momentum oscillator in the 0-100 range that, unlike RSI, weights price changes by volume: it shows how strongly volume backs each move. Values above 80 are considered overbought, below 20 - oversold. The alert fires when MFI enters or leaves the overbought/oversold zone.",
    Ru = "Импульсный осциллятор в диапазоне 0-100, который, в отличие от RSI, взвешивает изменения цены объёмом: показывает, насколько уверенно объём поддерживает движение. Значения выше 80 считаются перекупленностью, ниже 20 - перепроданностью. Алерт срабатывает, когда MFI входит в зону перекупленности/перепроданности или выходит из неё.",
    Es = "Un oscilador de momentum en el rango 0-100 que, a diferencia del RSI, pondera los cambios de precio por el volumen: muestra cuánto respalda el volumen cada movimiento. Valores por encima de 80 se consideran sobrecompra, por debajo de 20 - sobreventa. La alerta se activa cuando el MFI entra o sale de la zona de sobrecompra/sobreventa.",
    It = "Un oscillatore di momentum nell'intervallo 0-100 che, a differenza dell'RSI, pondera le variazioni di prezzo per il volume: mostra con quanta forza il volume sostiene ogni movimento. Valori sopra 80 sono considerati ipercomprato, sotto 20 - ipervenduto. L'avviso scatta quando l'MFI entra o esce dalla zona di ipercomprato/ipervenduto.",
    De = "Ein Momentum-Oszillator im Bereich 0-100, der im Gegensatz zum RSI die Preisänderungen mit dem Volumen gewichtet: Er zeigt, wie stark das Volumen jede Bewegung stützt. Werte über 80 gelten als überkauft, unter 20 als überverkauft. Der Alert wird ausgelöst, wenn der MFI in die Zone überkauft/überverkauft eintritt oder sie verlässt.",
    Fr = "Un oscillateur de momentum dans la plage 0-100 qui, contrairement au RSI, pondère les variations de prix par le volume : il montre avec quelle force le volume soutient chaque mouvement. Les valeurs au-dessus de 80 sont considérées comme surachetées, en dessous de 20 comme survendues. L'alerte se déclenche lorsque le MFI entre dans la zone de surachat/survente ou en sort.",
    PtBr = "Um oscilador de momentum na faixa 0-100 que, ao contrário do RSI, pondera as mudanças de preço pelo volume: mostra com que força o volume sustenta cada movimento. Valores acima de 80 são considerados sobrecomprados, abaixo de 20 - sobrevendidos. O alerta é acionado quando o MFI entra ou sai da zona de sobrecompra/sobrevenda.",
    Uk = "Імпульсний осцилятор у діапазоні 0-100, який, на відміну від RSI, зважує зміни ціни обсягом: показує, наскільки впевнено обсяг підтримує кожен рух. Значення вище 80 вважаються перекупленістю, нижче 20 - перепроданістю. Алерт спрацьовує, коли MFI входить у зону перекупленості/перепроданості або виходить з неї.",
    Be = "Імпульсны асцылятар у дыяпазоне 0-100, які, на адметнасць ад RSI, узважвае змены цаны аб'ёмам: паказвае, наколькі ўпэўнена аб'ём падтрымлівае кожны рух. Значэнні вышэй 80 лічацца перакупленасцю, ніжэй 20 - перепродадзенасцю. Апавяшчэнне спрацоўвае, калі MFI ўваходзіць у зону перакупленасці/перепродадзенасці або выходзіць з яе.")]
public class MfiIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
      En = "MFI calculation period in candles. Standard is 14.",
      Ru = "Период расчёта MFI в свечах. Стандарт - 14.",
      Es = "Periodo de cálculo del MFI en velas. El estándar es 14.",
      It = "Periodo di calcolo dell'MFI in candele. Lo standard è 14.",
      De = "Berechnungsperiode des MFI in Kerzen. Standard ist 14.",
      Fr = "Période de calcul du MFI en bougies. La valeur standard est 14.",
      PtBr = "Período de cálculo do MFI em velas. O padrão é 14.",
      Uk = "Період розрахунку MFI у свічках. Стандарт - 14.",
      Be = "Перыяд разліку MFI у свечках. Стандарт - 14.")]
  public int Period { get; set; } = 14;

  [ParameterHint(
      En = "Level above which MFI is considered overbought. The alert fires when MFI moves above or below this level.",
      Ru = "Уровень, выше которого MFI считается перекупленным. Алерт срабатывает при переходе MFI выше или ниже этого уровня.",
      Es = "Nivel por encima del cual el MFI se considera sobrecompra. La alerta se activa cuando el MFI sube por encima o baja por debajo de este nivel.",
      It = "Livello al di sopra del quale l'MFI è considerato ipercomprato. L'avviso scatta quando l'MFI sale sopra o scende sotto questo livello.",
      De = "Schwellenwert, oberhalb dessen der MFI als überkauft gilt. Der Alert wird ausgelöst, wenn der MFI über diesen Wert steigt oder darunter fällt.",
      Fr = "Niveau au-dessus duquel le MFI est considéré comme suracheté. L'alerte se déclenche lorsque le MFI passe au-dessus ou en dessous de ce niveau.",
      PtBr = "Nível acima do qual o MFI é considerado sobrecomprado. O alerta é acionado quando o MFI sobe acima ou cai abaixo desse nível.",
      Uk = "Рівень, вище якого MFI вважається перекупленим. Алерт спрацьовує, коли MFI піднімається вище або опускається нижче цього рівня.",
      Be = "Узровень, вышэй якога MFI лічыцца перакупленым. Апавяшчэнне спрацоўвае, калі MFI падымаецца вышэй або апускаецца ніжэй гэтага ўзроўню.")]
  public double Overbought { get; set; } = 80.0;

  [ParameterHint(
      En = "Level below which MFI is considered oversold. The alert fires when MFI moves below or above this level.",
      Ru = "Уровень, ниже которого MFI считается перепроданным. Алерт срабатывает при переходе MFI ниже или выше этого уровня.",
      Es = "Nivel por debajo del cual el MFI se considera sobreventa. La alerta se activa cuando el MFI baja por debajo o sube por encima de este nivel.",
      It = "Livello al di sotto del quale l'MFI è considerato ipervenduto. L'avviso scatta quando l'MFI scende sotto o sale sopra questo livello.",
      De = "Schwellenwert, unterhalb dessen der MFI als überverkauft gilt. Der Alert wird ausgelöst, wenn der MFI unter diesen Wert fällt oder darüber steigt.",
      Fr = "Niveau en dessous duquel le MFI est considéré comme survendu. L'alerte se déclenche lorsque le MFI passe en dessous ou au-dessus de ce niveau.",
      PtBr = "Nível abaixo do qual o MFI é considerado sobrevendido. O alerta é acionado quando o MFI cai abaixo ou sobe acima desse nível.",
      Uk = "Рівень, нижче якого MFI вважається перепроданим. Алерт спрацьовує, коли MFI опускається нижче або піднімається вище цього рівня.",
      Be = "Узровень, ніжэй якога MFI лічыцца перепродадзеным. Апавяшчэнне спрацоўвае, калі MFI апускаецца ніжэй або падымаецца вышэй гэтага ўзроўню.")]
  public double Oversold { get; set; } = 20.0;

  public int GetCandleCount() => Period * 2;
}
