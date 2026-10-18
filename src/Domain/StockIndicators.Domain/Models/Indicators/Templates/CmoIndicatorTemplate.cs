using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "A momentum oscillator on the -100 to 100 scale: the raw balance of upward and downward changes over the window, without smoothing. It reaches the extremes earlier than RSI, so ±50 mark strong impulses. The alert fires when CMO enters or leaves the overbought/oversold zone.",
    Ru = "Импульсный осциллятор в шкале от -100 до 100: несглаженный баланс движений вверх и вниз за окно. Доходит до экстремумов раньше RSI, поэтому ±50 отмечают сильные импульсы. Алерт срабатывает, когда CMO входит в зону перекупленности/перепроданности или выходит из неё.",
    Es = "Un oscilador de momentum en la escala de -100 a 100: el balance sin suavizar de los cambios al alza y a la baja en la ventana. Llega a los extremos antes que el RSI, por eso ±50 marcan impulsos fuertes. La alerta se activa cuando el CMO entra o sale de la zona de sobrecompra/sobreventa.",
    It = "Un oscillatore di momentum su scala da -100 a 100: il bilancio grezzo dei rialzi e dei ribassi sulla finestra, senza smoothing. Raggiunge gli estremi prima dell'RSI, quindi ±50 segnalano forti impulsi. L'avviso scatta quando il CMO entra o esce dalla zona di ipercomprato/ipervenduto.",
    De = "Ein Momentum-Oszillator auf der Skala von -100 bis 100: die rohe Bilanz der Aufwärts- und Abwärtsbewegungen im Fenster, ohne Glättung. Er erreicht die Extreme früher als der RSI, daher markieren ±50 starke Impulse. Der Alert wird ausgelöst, wenn der CMO die Überkauft-/Überverkauft-Zone betritt oder verlässt.",
    Fr = "Un oscillateur de momentum sur l'échelle de -100 à 100 : la balance brute des hausses et des baisses sur la fenêtre, sans lissage. Il atteint les extrêmes plus tôt que le RSI, donc ±50 marquent de fortes impulsions. L'alerte se déclenche quand le CMO entre dans la zone de surachat/survente ou en sort.",
    PtBr = "Um oscilador de momentum na escala de -100 a 100: o balanço bruto das altas e baixas na janela, sem suavização. Ele chega aos extremos antes do RSI, então ±50 marcam impulsos fortes. O alerta é acionado quando o CMO entra ou sai da zona de sobrecompra/sobrevenda.",
    Uk = "Імпульсний осциллятор у шкалі від -100 до 100: баланс рухів угору та вниз за вікно без згладжування. Досягає екстремумів раніше за RSI, тому ±50 позначають сильні імпульси. Алерт спрацьовує, коли CMO входить у зону перекупленості/перепроданості або виходить із неї.",
    Be = "Імпульсны асцылятар у шкале ад -100 да 100: баланс росту і падзення за акна без згладжвання. Дасягае экстрэмумаў раней за RSI, таму ±50 адзначаюць моцныя імпульсы. Апавяшчэнне спрацоўвае, калі CMO ўваходзіць у зону перакупленасці/перепродадзенасці або выходзіць з яе.")]
public class CmoIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
{
  [ParameterHint(
      En = "Candle timeframe the indicator is calculated on. The alert is evaluated on each closed candle of this period.",
      Ru = "Таймфрейм свечей, по которым считается индикатор. Проверка выполняется при закрытии каждой свечи этого периода.",
      Es = "Temporalidad de las velas con la que se calcula el indicador. La alerta se evalúa al cierre de cada vela de este periodo.",
      It = "Timeframe delle candele su cui viene calcolato l'indicatore. L'avviso viene valutato alla chiusura di ogni candela di questo periodo.",
      De = "Zeitrahmen der Kerzen, für den der Indikator berechnet wird. Der Alert wird bei jeder geschlossenen Kerze dieses Zeitraums ausgewertet.",
      Fr = "Unité de temps des bougies sur laquelle l'indicateur est calculé. L'alerte est évaluée à la clôture de chaque bougie de cette période.",
      PtBr = "Timeframe das velas no qual o indicador é calculado. O alerta é avaliado no fechamento de cada vela deste período.",
      Uk = "Таймфрейм свічок, за яким розраховується індикатор. Алерт перевіряється при закритті кожної свічки цього періоду.",
      Be = "Таймфрейм свічак, па якіх разлічваецца індыкатар. Апавяшчэнне праверяецца пры закрыцці кожнай свічкі гэтага перыяду.")]
  public TimeFrame TimeFrame { get; set; }

  [ParameterHint(
      En = "CMO calculation period in candles. Standard is 20.",
      Ru = "Период расчёта CMO в свечах. Стандарт - 20.",
      Es = "Periodo de cálculo del CMO en velas. El estándar es 20.",
      It = "Periodo di calcolo del CMO in candele. Lo standard è 20.",
      De = "CMO-Berechnungszeitraum in Kerzen. Standard ist 20.",
      Fr = "Période de calcul du CMO en bougies. La valeur standard est 20.",
      PtBr = "Período de cálculo do CMO em velas. O padrão é 20.",
      Uk = "Період розрахунку CMO у свічках. Стандарт - 20.",
      Be = "Перыяд разліку CMO у свачках. Стандарт - 20.")]
  public int Period { get; set; } = 20;

  [ParameterHint(
      En = "Level above which CMO is considered overbought. The alert fires when CMO moves above or below this level.",
      Ru = "Уровень, выше которого CMO считается перекупленным. Алерт срабатывает при переходе CMO выше или ниже этого уровня.",
      Es = "Nivel por encima del cual el CMO se considera sobrecompra. La alerta se activa cuando el CMO sube por encima o baja por debajo de este nivel.",
      It = "Livello al di sopra del quale il CMO è considerato ipercomprato. L'avviso scatta quando il CMO sale sopra o scende sotto questo livello.",
      De = "Schwellenwert, oberhalb dessen der CMO als überkauft gilt. Der Alert wird ausgelöst, wenn der CMO über diesen Wert steigt oder unter ihn fällt.",
      Fr = "Niveau au-dessus duquel le CMO est considéré suracheté. L'alerte se déclenche quand le CMO passe au-dessus ou en dessous de ce niveau.",
      PtBr = "Nível acima do qual o CMO é considerado sobrecomprado. O alerta é acionado quando o CMO sobe acima ou cai abaixo deste nível.",
      Uk = "Рівень, вище за який CMO вважається перекупленим. Алерт спрацьовує, коли CMO піднімається вище або опускається нижче за цей рівень.",
      Be = "Узровень, вышэй за які CMO лічыцца перакупленым. Апавяшчэнне спрацоўвае, калі CMO падымаецца вышэй або апускаецца ніжэй за гэты ўзровень.")]
  public double Overbought { get; set; } = 50.0;

  [AllowNegativeValue]
  [ParameterHint(
      En = "Level below which CMO is considered oversold. The alert fires when CMO moves below or above this level.",
      Ru = "Уровень, ниже которого CMO считается перепроданным. Алерт срабатывает при переходе CMO ниже или выше этого уровня.",
      Es = "Nivel por debajo del cual el CMO se considera sobreventa. La alerta se activa cuando el CMO baja por debajo o sube por encima de este nivel.",
      It = "Livello al di sotto del quale il CMO è considerato ipervenduto. L'avviso scatta quando il CMO scende sotto o sale sopra questo livello.",
      De = "Schwellenwert, unterhalb dessen der CMO als überverkauft gilt. Der Alert wird ausgelöst, wenn der CMO unter diesen Wert fällt oder über ihn steigt.",
      Fr = "Niveau en dessous duquel le CMO est considéré survendu. L'alerte se déclenche quand le CMO passe en dessous ou remonte au-dessus de ce niveau.",
      PtBr = "Nível abaixo do qual o CMO é considerado sobrevendido. O alerta é acionado quando o CMO cai abaixo ou sobe acima deste nível.",
      Uk = "Рівень, нижче за який CMO вважається перепроданим. Алерт спрацьовує, коли CMO опускається нижче або піднімається вище за цей рівень.",
      Be = "Узровень, ніжэй за які CMO лічыцца перепродадзеным. Апавяшчэнне спрацоўвае, калі CMO апускаецца ніжэй або падымаецца вышэй за гэты ўзровень.")]
  public double Oversold { get; set; } = -50.0;

  public int GetCandleCount() => Period * 2;
}
