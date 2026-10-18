using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "A momentum oscillator that measures the deviation of the typical price from its moving average, scaled by the mean deviation. Unlike bounded oscillators it has no fixed range: levels around +100 and -100 mark strong impulse moves. The alert fires when CCI enters or leaves the overbought/oversold zone.",
    Ru = "Импульсный осциллятор, измеряющий отклонение типичной цены от её средней, нормированный на среднее отклонение. В отличие от ограниченных осцилляторов не имеет фиксированного диапазона: уровни около +100 и -100 отмечают сильные импульсные движения. Алерт срабатывает, когда CCI входит в зону перекупленности/перепроданности или выходит из неё.",
    Es = "Un oscilador de momentum que mide la desviación del precio típico respecto a su media móvil, escalado por la desviación media. A diferencia de los osciladores acotados no tiene rango fijo: los niveles en torno a +100 y -100 marcan impulsos fuertes. La alerta se activa cuando el CCI entra o sale de la zona de sobrecompra/sobreventa.",
    It = "Un oscillatore di momentum che misura la deviazione del prezzo tipico dalla sua media mobile, rapportata alla deviazione media. A differenza degli oscillatori limitati non ha un range fisso: i livelli intorno a +100 e -100 segnalano forti impulsi. L'avviso scatta quando il CCI entra o esce dalla zona di ipercomprato/ipervenduto.",
    De = "Ein Momentum-Oszillator, der die Abweichung des typischen Kurses von seinem gleitenden Durchschnitt misst, normiert auf die mittlere Abweichung. Anders als begrenzte Oszillatoren hat er keinen festen Bereich: Marken um +100 und -100 kennzeichnen starke Impulse. Der Alert wird ausgelöst, wenn der CCI die Überkauft-/Überverkauft-Zone betritt oder verlässt.",
    Fr = "Un oscillateur de momentum qui mesure l'écart du prix typique par rapport à sa moyenne mobile, normalisé par l'écart moyen. Contrairement aux oscillateurs bornés, il n'a pas de plage fixe : les niveaux autour de +100 et -100 marquent de fortes impulsions. L'alerte se déclenche quand le CCI entre dans la zone de surachat/survente ou en sort.",
    PtBr = "Um oscilador de momentum que mede o desvio do preço típico em relação à sua média móvel, escalonado pelo desvio médio. Diferentemente dos osciladores limitados, não tem faixa fixa: níveis em torno de +100 e -100 marcam impulsos fortes. O alerta é acionado quando o CCI entra ou sai da zona de sobrecompra/sobrevenda.",
    Uk = "Імпульсний осциллятор, що вимірює відхилення типової ціни від її ковзної середньої, нормоване на середнє відхилення. На відміну від обмежених осциляторів, не має фіксованого діапазону: рівні близько +100 і -100 позначають сильні імпульсні рухи. Алерт спрацьовує, коли CCI входить у зону перекупленості/перепроданості або виходить із неї.",
    Be = "Імпульсны асцылятар, які вымярае адхіленне тыповага кошту ад яго слізгальнай сярэдняй, нармаванае на сярэдняе адхіленне. На адрозненне ад абмежаваных асцылятараў, не мае фіксаванага дыяпазону: узроўні каля +100 і -100 адзначаюць моцныя імпульсныя рухі. Апавяшчэнне спрацоўвае, калі CCI ўваходзіць у зону перакупленасці/перепродадзенасці або выходзіць з яе.")]
public class CciIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
      En = "CCI calculation period in candles. Standard is 20.",
      Ru = "Период расчёта CCI в свечах. Стандарт - 20.",
      Es = "Periodo de cálculo del CCI en velas. El estándar es 20.",
      It = "Periodo di calcolo del CCI in candele. Lo standard è 20.",
      De = "CCI-Berechnungszeitraum in Kerzen. Standard ist 20.",
      Fr = "Période de calcul du CCI en bougies. La valeur standard est 20.",
      PtBr = "Período de cálculo do CCI em velas. O padrão é 20.",
      Uk = "Період розрахунку CCI у свічках. Стандарт - 20.",
      Be = "Перыяд разліку CCI у свачках. Стандарт - 20.")]
  public int Period { get; set; } = 20;

  [ParameterHint(
      En = "Level above which CCI is considered overbought. The alert fires when CCI moves above or below this level.",
      Ru = "Уровень, выше которого CCI считается перекупленным. Алерт срабатывает при переходе CCI выше или ниже этого уровня.",
      Es = "Nivel por encima del cual el CCI se considera sobrecompra. La alerta se activa cuando el CCI sube por encima o baja por debajo de este nivel.",
      It = "Livello al di sopra del quale il CCI è considerato ipercomprato. L'avviso scatta quando il CCI sale sopra o scende sotto questo livello.",
      De = "Schwellenwert, oberhalb dessen der CCI als überkauft gilt. Der Alert wird ausgelöst, wenn der CCI über diesen Wert steigt oder unter ihn fällt.",
      Fr = "Niveau au-dessus duquel le CCI est considéré suracheté. L'alerte se déclenche quand le CCI passe au-dessus ou en dessous de ce niveau.",
      PtBr = "Nível acima do qual o CCI é considerado sobrecomprado. O alerta é acionado quando o CCI sobe acima ou cai abaixo deste nível.",
      Uk = "Рівень, вище за який CCI вважається перекупленим. Алерт спрацьовує, коли CCI піднімається вище або опускається нижче за цей рівень.",
      Be = "Узровень, вышэй за які CCI лічыцца перакупленым. Апавяшчэнне спрацоўвае, калі CCI падымаецца вышэй або апускаецца ніжэй за гэты ўзровень.")]
  public double Overbought { get; set; } = 100.0;

  [AllowNegativeValue]
  [ParameterHint(
      En = "Level below which CCI is considered oversold. The alert fires when CCI moves below or above this level.",
      Ru = "Уровень, ниже которого CCI считается перепроданным. Алерт срабатывает при переходе CCI ниже или выше этого уровня.",
      Es = "Nivel por debajo del cual el CCI se considera sobreventa. La alerta se activa cuando el CCI baja por debajo o sube por encima de este nivel.",
      It = "Livello al di sotto del quale il CCI è considerato ipervenduto. L'avviso scatta quando il CCI scende sotto o sale sopra questo livello.",
      De = "Schwellenwert, unterhalb dessen der CCI als überverkauft gilt. Der Alert wird ausgelöst, wenn der CCI unter diesen Wert fällt oder über ihn steigt.",
      Fr = "Niveau en dessous duquel le CCI est considéré survendu. L'alerte se déclenche quand le CCI passe en dessous ou remonte au-dessus de ce niveau.",
      PtBr = "Nível abaixo do qual o CCI é considerado sobrevendido. O alerta é acionado quando o CCI cai abaixo ou sobe acima deste nível.",
      Uk = "Рівень, нижче за який CCI вважається перепроданим. Алерт спрацьовує, коли CCI опускається нижче або піднімається вище за цей рівень.",
      Be = "Узровень, ніжэй за які CCI лічыцца перепродадзеным. Апавяшчэнне спрацоўвае, калі CCI апускаецца ніжэй або падымаецца вышэй за гэты ўзровень.")]
  public double Oversold { get; set; } = -100.0;

  public int GetCandleCount() => Period * 2;
}
