using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "A momentum oscillator on the -100 to 0 scale (the traditional stochastic, inverted): it compares the close to the highest high of the window. Readings above -20 mark the top of the range, below -80 the bottom. The alert fires when Williams %R enters or leaves the overbought/oversold zone.",
    Ru = "Импульсный осциллятор в шкале от -100 до 0 (столхастический осциллятор с обратным знаком): сравнивает закрытие с максимумом окна. Значения выше -20 отмечают верх диапазона, ниже -80 - низ. Алерт срабатывает, когда Williams %R входит в зону перекупленности/перепроданности или выходит из неё.",
    Es = "Un oscilador de momentum en la escala de -100 a 0 (el estocástico tradicional, invertido): compara el cierre con el máximo más alto de la ventana. Lecturas por encima de -20 marcan el techo del rango, por debajo de -80 el suelo. La alerta se activa cuando el Williams %R entra o sale de la zona de sobrecompra/sobreventa.",
    It = "Un oscillatore di momentum su scala da -100 a 0 (lo stocastico tradizionale, invertito): confronta la chiusura con il massimo più alto della finestra. Letture sopra -20 segnano il tetto dell'intervallo, sotto -80 il fondo. L'avviso scatta quando il Williams %R entra o esce dalla zona di ipercomprato/ipervenduto.",
    De = "Ein Momentum-Oszillator auf der Skala von -100 bis 0 (der klassische Stochastik-Oszillator, invertiert): Er vergleicht den Schlusskurs mit dem höchsten Hoch des Fensters. Werte über -20 markieren die Obergrenze der Spanne, unter -80 die Untergrenze. Der Alert wird ausgelöst, wenn Williams %R in die Überkauft-/Überverkauft-Zone eintritt oder sie verlässt.",
    Fr = "Un oscillateur de momentum sur l'échelle de -100 à 0 (le stochastique traditionnel, inversé) : il compare la clôture au plus haut atteint dans la fenêtre. Les valeurs au-dessus de -20 marquent le sommet de la plage, en dessous de -80 le bas. L'alerte se déclenche quand Williams %R entre dans la zone de surachat/survente ou en sort.",
    PtBr = "Um oscilador de momentum na escala de -100 a 0 (o estocástico tradicional, invertido): compara o fechamento com a máxima mais alta da janela. Leituras acima de -20 marcam o topo do intervalo, abaixo de -80 a base. O alerta é acionado quando o Williams %R entra ou sai da zona de sobrecompra/sobrevenda.",
    Uk = "Імпульсний осциллятор у шкалі від -100 до 0 (традиційний стохастичний осциллятор, інвертований): порівнює закриття з найвищим максимумом вікна. Значення вище -20 позначають верх діапазону, нижче -80 - низ. Алерт спрацьовує, коли Williams %R входить у зону перекупленості/перепроданості або виходить із неї.",
    Be = "Імпульсны асцылятар у шкале ад -100 да 0 (традыцыйны стахастычны асцылятар, інвертаваны): параўноўвае закрыццё з найвышэйшым максімумам акна. Значэнні вышэй за -20 пазначаюць верх дыяпазону, ніжэй за -80 - яго дол. Апавяшчэнне спрацоўвае, калі Williams %R уваходзіць у зону перакупленасці/перепродадзенасці або выходзіць з яе.")]
public class WilliamsRIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
      Be = "Таймфрэйм свічак, на якім разлічваецца індыкатар. Апавяшчэнне правяраецца пры закрыцці кожнай свічкі гэтага перыяду.")]
  public TimeFrame TimeFrame { get; set; }

  [ParameterHint(
      En = "Williams %R calculation period in candles. Standard is 14.",
      Ru = "Период расчёта Williams %R в свечах. Стандарт - 14.",
      Es = "Periodo de cálculo del Williams %R en velas. El estándar es 14.",
      It = "Periodo di calcolo del Williams %R in candele. Lo standard è 14.",
      De = "Berechnungsperiode des Williams %R in Kerzen. Standard ist 14.",
      Fr = "Période de calcul du Williams %R en bougies. La valeur standard est 14.",
      PtBr = "Período de cálculo do Williams %R em velas. O padrão é 14.",
      Uk = "Період розрахунку Williams %R у свічках. Стандарт - 14.",
      Be = "Перыяд разліку Williams %R у свечках. Стандарт - 14.")]
  public int Period { get; set; } = 14;

  [AllowNegativeValue]
  [ParameterHint(
      En = "Level above which Williams %R is considered overbought, on the negative -100..0 scale. The alert fires when the indicator moves above or below this level.",
      Ru = "Уровень, выше которого Williams %R считается перекупленным, в отрицательной шкале -100..0. Алерт срабатывает при переходе индикатора выше или ниже этого уровня.",
      Es = "Nivel por encima del cual el Williams %R se considera sobrecompra, en la escala negativa -100..0. La alerta se activa cuando el indicador sube por encima o baja por debajo de este nivel.",
      It = "Livello al di sopra del quale il Williams %R è considerato ipercomprato, sulla scala negativa -100..0. L'avviso scatta quando l'indicatore sale sopra o scende sotto questo livello.",
      De = "Wert, über dem Williams %R als überkauft gilt, auf der negativen Skala -100..0. Der Alert wird ausgelöst, wenn der Indikator über diesen Wert steigt oder darunter fällt.",
      Fr = "Niveau au-dessus duquel Williams %R est considéré comme suracheté, sur l'échelle négative -100..0. L'alerte se déclenche quand l'indicateur passe au-dessus ou en dessous de ce niveau.",
      PtBr = "Nível acima do qual o Williams %R é considerado sobrecomprado, na escala negativa -100..0. O alerta é acionado quando o indicador sobe acima ou cai abaixo desse nível.",
      Uk = "Рівень, вище за який Williams %R вважається перекупленим, на від'ємній шкалі -100..0. Алерт спрацьовує, коли індикатор піднімається вище або опускається нижче цього рівня.",
      Be = "Узровень, вышэй за які Williams %R лічыцца перакупленым, на адмоўнай шкале -100..0. Апавяшчэнне спрацоўвае, калі індыкатар падымаецца вышэй або апускаецца ніжэй за гэты ўзровень.")]
  public double Overbought { get; set; } = -20.0;

  [AllowNegativeValue]
  [ParameterHint(
      En = "Level below which Williams %R is considered oversold, on the negative -100..0 scale. The alert fires when the indicator moves below or above this level.",
      Ru = "Уровень, ниже которого Williams %R считается перепроданным, в отрицательной шкале -100..0. Алерт срабатывает при переходе индикатора ниже или выше этого уровня.",
      Es = "Nivel por debajo del cual el Williams %R se considera sobreventa, en la escala negativa -100..0. La alerta se activa cuando el indicador baja por debajo o sube por encima de este nivel.",
      It = "Livello al di sotto del quale il Williams %R è considerato ipervenduto, sulla scala negativa -100..0. L'avviso scatta quando l'indicatore scende sotto o sale sopra questo livello.",
      De = "Wert, unter dem Williams %R als überverkauft gilt, auf der negativen Skala -100..0. Der Alert wird ausgelöst, wenn der Indikator unter diesen Wert fällt oder darüber steigt.",
      Fr = "Niveau en dessous duquel Williams %R est considéré comme survendu, sur l'échelle négative -100..0. L'alerte se déclenche quand l'indicateur passe en dessous ou au-dessus de ce niveau.",
      PtBr = "Nível abaixo do qual o Williams %R é considerado sobrevendido, na escala negativa -100..0. O alerta é acionado quando o indicador cai abaixo ou sobe acima desse nível.",
      Uk = "Рівень, нижче якого Williams %R вважається перепроданим, на від'ємній шкалі -100..0. Алерт спрацьовує, коли індикатор опускається нижче або піднімається вище цього рівня.",
      Be = "Узровень, ніжэй за які Williams %R лічыцца перепродадзеным, на адмоўнай шкале -100..0. Апавяшчэнне спрацоўвае, калі індыкатар апускаецца ніжэй або падымаецца вышэй за гэты ўзровень.")]
  public double Oversold { get; set; } = -80.0;

  public int GetCandleCount() => Period * 2;
}
