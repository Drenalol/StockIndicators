using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "A momentum oscillator in the 0-100 range that measures the speed of price changes. Values above 70 are traditionally considered overbought, below 30 - oversold. The alert fires when RSI enters or leaves the overbought/oversold zone.",
    Ru = "Импульсный осциллятор в диапазоне 0-100, измеряющий скорость изменения цены. Значения выше 70 традиционно считаются перекупленностью, ниже 30 - перепроданностью. Алерт срабатывает, когда RSI входит в зону перекупленности/перепроданности или выходит из неё.",
    Es = "Un oscilador de momentum en el rango 0-100 que mide la velocidad de los cambios de precio. Valores por encima de 70 se consideran tradicionalmente sobrecompra, por debajo de 30 - sobreventa. La alerta se activa cuando el RSI entra o sale de la zona de sobrecompra/sobreventa.",
    It = "Oscillatore di momentum nell'intervallo 0-100 che misura la velocità delle variazioni di prezzo. Valori sopra 70 sono tradizionalmente considerati ipercomprato, sotto 30 - ipervenduto. L'avviso scatta quando l'RSI entra o esce dalla zona di ipercomprato/ipervenduto.",
    De = "Ein Momentum-Oszillator im Bereich 0-100, der die Geschwindigkeit der Preisänderungen misst. Werte über 70 gelten traditionell als überkauft, unter 30 als überverkauft. Der Alert wird ausgelöst, wenn der RSI in die überkaufte/überverkaufte Zone eintritt oder sie verlässt.",
    Fr = "Un oscillateur de momentum dans la plage 0-100 qui mesure la vitesse des variations de prix. Les valeurs au-dessus de 70 sont traditionnellement considérées comme surachetées, en dessous de 30 comme survendues. L'alerte se déclenche lorsque le RSI entre dans la zone de surachat/survente ou en sort.",
    PtBr = "Um oscilador de momentum na faixa 0-100 que mede a velocidade das variações de preço. Valores acima de 70 são tradicionalmente considerados sobrecomprados, abaixo de 30 - sobrevendidos. O alerta é acionado quando o RSI entra ou sai da zona de sobrecompra/sobrevenda.",
    Uk = "Імпульсний осцилятор у діапазоні 0-100, що вимірює швидкість зміни ціни. Значення вище 70 традиційно вважаються перекупленістю, нижче 30 - перепроданістю. Алерт спрацьовує, коли RSI входить у зону перекупленості/перепроданості або виходить з неї.",
    Be = "Імпульсны асцылятар у дыяпазоне 0-100, які вымярае хуткасць змены цаны. Значэнні вышэй 70 традыцыйна лічацца перакупленасцю, ніжэй 30 - перепродадзенасцю. Апавяшчэнне спрацоўвае, калі RSI ўваходзіць у зону перакупленасці/перепродадзенасці або выходзіць з яе.")]
public class RsiIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
      En = "RSI calculation period in candles. Standard is 14.",
      Ru = "Период расчёта RSI в свечах. Стандарт - 14.",
      Es = "Periodo de cálculo del RSI en velas. El estándar es 14.",
      It = "Periodo di calcolo dell'RSI in candele. Lo standard è 14.",
      De = "Berechnungsperiode des RSI in Kerzen. Standard ist 14.",
      Fr = "Période de calcul du RSI en bougies. La valeur standard est 14.",
      PtBr = "Período de cálculo do RSI em velas. O padrão é 14.",
      Uk = "Період розрахунку RSI у свічках. Стандарт - 14.",
      Be = "Перыяд разліку RSI у свечках. Стандарт - 14.")]
  public int Period { get; set; } = 14;

  [ParameterHint(
      En = "Level above which RSI is considered overbought. The alert fires when RSI moves above or below this level.",
      Ru = "Уровень, выше которого RSI считается перекупленным. Алерт срабатывает при переходе RSI выше или ниже этого уровня.",
      Es = "Nivel por encima del cual el RSI se considera sobrecompra. La alerta se activa cuando el RSI sube por encima o baja por debajo de este nivel.",
      It = "Livello al di sopra del quale l'RSI è considerato ipercomprato. L'avviso scatta quando l'RSI sale sopra o scende sotto questo livello.",
      De = "Schwellenwert, oberhalb dessen der RSI als überkauft gilt. Der Alert wird ausgelöst, wenn der RSI über diesen Wert steigt oder darunter fällt.",
      Fr = "Niveau au-dessus duquel le RSI est considéré comme suracheté. L'alerte se déclenche lorsque le RSI passe au-dessus ou en dessous de ce niveau.",
      PtBr = "Nível acima do qual o RSI é considerado sobrecomprado. O alerta é acionado quando o RSI sobe acima ou cai abaixo desse nível.",
      Uk = "Рівень, вище якого RSI вважається перекупленим. Алерт спрацьовує, коли RSI піднімається вище або опускається нижче цього рівня.",
      Be = "Узровень, вышэй якога RSI лічыцца перакупленым. Апавяшчэнне спрацоўвае, калі RSI падымаецца вышэй або апускаецца ніжэй гэтага ўзроўню.")]
  public double Overbought { get; set; } = 70;

  [ParameterHint(
      En = "Level below which RSI is considered oversold. The alert fires when RSI moves below or above this level.",
      Ru = "Уровень, ниже которого RSI считается перепроданным. Алерт срабатывает при переходе RSI ниже или выше этого уровня.",
      Es = "Nivel por debajo del cual el RSI se considera sobreventa. La alerta se activa cuando el RSI baja por debajo o sube por encima de este nivel.",
      It = "Livello al di sotto del quale l'RSI è considerato ipervenduto. L'avviso scatta quando l'RSI scende sotto o sale sopra questo livello.",
      De = "Schwellenwert, unterhalb dessen der RSI als überverkauft gilt. Der Alert wird ausgelöst, wenn der RSI unter diesen Wert fällt oder darüber steigt.",
      Fr = "Niveau en dessous duquel le RSI est considéré comme survendu. L'alerte se déclenche lorsque le RSI passe en dessous ou au-dessus de ce niveau.",
      PtBr = "Nível abaixo do qual o RSI é considerado sobrevendido. O alerta é acionado quando o RSI cai abaixo ou sobe acima desse nível.",
      Uk = "Рівень, нижче якого RSI вважається перепроданим. Алерт спрацьовує, коли RSI опускається нижче або піднімається вище цього рівня.",
      Be = "Узровень, ніжэй якога RSI лічыцца перепродадзеным. Апавяшчэнне спрацоўвае, калі RSI апускаецца ніжэй або падымаецца вышэй гэтага ўзроўню.")]
  public double Oversold { get; set; } = 30;

  [ParameterHint(
      En = "Which zone fires the alert: Up - crossings of the Overbought level only, Down - crossings of the Oversold level only, Both - both zones.",
      Ru = "Какая зона даёт алерт: «Рост» - только переходы уровня перекупленности, «Падение» - только переходы уровня перепроданности, «Оба» - обе зоны.",
      Es = "Qué zona activa la alerta: «Subida» - solo cruces del nivel de sobrecompra, «Caída» - solo cruces del nivel de sobreventa, «Ambos» - las dos zonas.",
      It = "Quale zona attiva l'avviso: Up - solo gli incroci del livello Overbought, Down - solo gli incroci del livello Oversold, Both - entrambe le zone.",
      De = "Welche Zone den Alert auslöst: Up - nur Kreuzungen des Overbought-Werts, Down - nur Kreuzungen des Oversold-Werts, Both - beide Zonen.",
      Fr = "Quelle zone déclenche l'alerte : Up - uniquement les croisements du niveau Overbought, Down - uniquement les croisements du niveau Oversold, Both - les deux zones.",
      PtBr = "Qual zona aciona o alerta: Up - apenas cruzamentos do nível Overbought, Down - apenas cruzamentos do nível Oversold, Both - as duas zonas.",
      Uk = "Яка зона дає алерт: Up - лише перетини рівня Overbought, Down - лише перетини рівня Oversold, Both - обидві зони.",
      Be = "Якая зона дае апавяшчэнне: Up - толькі перасячэнні ўзроўню Overbought, Down - толькі перасячэнні ўзроўню Oversold, Both - абедзве зоны.")]
  public AlertDirection Direction { get; set; } = AlertDirection.Both;

  public int GetCandleCount() => Period * 2;
}
