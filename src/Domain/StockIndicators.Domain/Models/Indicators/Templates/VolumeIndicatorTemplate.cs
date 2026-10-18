using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "The raw traded volume of the closed candle compared to its recent average. Volume confirms a move: a breakout on several times the average volume is far more meaningful than one on flat volume. The alert fires when the candle volume reaches the given multiple of the average of the previous candles. Unlike OBV, this is raw volume, not a cumulative balance.",
    Ru = "Сырой объём закрытой свечи в сравнении с её недавним средним. Объём подтверждает движение: пробой на объёме в несколько средних выглядит совсем иначе, чем пробой на пустом объёме. Алерт срабатывает, когда объём свечи достигает указанного количества средних от предыдущих свечей. В отличие от OBV, здесь сырой объём, а не накопленный баланс.",
    Es = "El volumen bruto de la vela cerrada comparado con su media reciente. El volumen confirma el movimiento: un breakout con varias veces el volumen medio es mucho más significativo que uno con volumen plano. La alerta se activa cuando el volumen de la vela alcanza el múltiplo indicado de la media de las velas anteriores. A diferencia del OBV, es volumen bruto, no un balance acumulado.",
    It = "Il volume grezzo scambiato della candela chiusa, confrontato con la sua media recente. Il volume conferma il movimento: una rottura su un volume più volte superiore alla media è molto più significativa di una su volumi piatti. L'avviso scatta quando il volume della candela raggiunge il multiplo indicato della media delle candele precedenti. A differenza dell'OBV, qui è volume grezzo, non un saldo cumulativo.",
    De = "Das rohe gehandelte Volumen der geschlossenen Kerze im Vergleich zu ihrem jüngsten Durchschnitt. Das Volumen bestätigt die Bewegung: ein Ausbruch bei einem Mehrfachen des Durchschnittsvolumens ist wesentlich aussagekräftiger als einer bei flachem Volumen. Der Alert wird ausgelöst, wenn das Kerzenvolumen das angegebene Vielfache des Durchschnitts der vorherigen Kerzen erreicht. Im Gegensatz zum OBV ist hier das rohe Volumen gemeint, kein kumulierter Saldo.",
    Fr = "Le volume brut échangé sur la bougie clôturée, comparé à sa moyenne récente. Le volume confirme le mouvement : une rupture sur un volume plusieurs fois supérieur à la moyenne est bien plus significative qu'une rupture sur un volume plat. L'alerte se déclenche quand le volume de la bougie atteint le multiple indiqué de la moyenne des bougies précédentes. Contrairement à l'OBV, il s'agit ici du volume brut, pas d'un solde cumulé.",
    PtBr = "O volume bruto negociado da vela fechada comparado à sua média recente. O volume confirma o movimento: uma ruptura com volume várias vezes acima da média é muito mais relevante do que uma com volume baixo. O alerta é acionado quando o volume da vela atinge o múltiplo indicado da média das velas anteriores. Ao contrário do OBV, aqui é volume bruto, não um saldo acumulado.",
    Uk = "Сирий обсяг закритої свічки порівняно з її недавнім середнім. Обсяг підтверджує рух: пробій на обсязі в кілька разів вищому за середній, набагато значущіший, ніж пробій на слабкому обсязі. Алерт спрацьовує, коли обсяг свічки досягає зазначеної кратності середнього попередніх свічок. На відміну від OBV, тут сирий обсяг, а не накопичений баланс.",
    Be = "Сыры аб'ём закрытай свечкі ў параўнанні з яе нядаўнім сярэднім. Аб'ём пацвярджае рух: прабой на аб'ёме ў некалькі разоў вышэйшым за сярэдні значна больш пераканаўчы, чым прабой на слабым аб'ёме. Апавяшчэнне спрацоўвае, калі аб'ём свечкі дасягае зададзенай кратнасці сярэдняга папярэдніх свечак. На адрозненне ад OBV, тут сыры аб'ём, а не накоплены баланс.")]
public class VolumeIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
      En = "How many previous candles the average volume is computed over. The latest candle's own volume is excluded from the average.",
      Ru = "Сколько предыдущих свечей участвует в среднем объёме. Объём последней свечи в среднее не входит.",
      Es = "Cuántas velas anteriores forman la media de volumen. El volumen de la última vela no entra en la media.",
      It = "Quante candele precedenti compongono la media del volume. Il volume dell'ultima candela non è incluso nella media.",
      De = "Anzahl der vorherigen Kerzen, über die das Durchschnittsvolumen berechnet wird. Das Volumen der letzten Kerze bleibt dabei außen vor.",
      Fr = "Nombre de bougies précédentes servant à calculer le volume moyen. Le volume de la dernière bougie lui-même est exclu de la moyenne.",
      PtBr = "Quantidade de velas anteriores usadas para calcular o volume médio. O volume da própria última vela fica de fora da média.",
      Uk = "Скільки попередніх свічок береться до розрахунку середнього обсягу. Обсяг останньої свічки в середнє не входить.",
      Be = "Колькі папярэдніх свечак утвараюць сярэдні аб'ём. Аб'ём апошняй свечкі ў сярэдняе не ўваходзіць.")]
  public int Periods { get; set; } = 20;

  [ParameterHint(
      En = "How many times the average volume the candle volume must reach to fire the alert. 2 means double the average.",
      Ru = "Во сколько раз объём свечи должен превысить среднее, чтобы сработал алерт. 2 - двойное превышение.",
      Es = "Cuántas veces la media debe alcanzar el volumen de la vela para activar la alerta. 2 significa el doble de la media.",
      It = "Per quante volte la media dei volumi deve essere raggiunta dal volume della candela per attivare l'avviso. 2 significa il doppio della media.",
      De = "Das Vielfache des Durchschnittsvolumens, das die Kerze erreichen muss, damit der Alert ausgelöst wird. 2 bedeutet das Doppelte des Durchschnitts.",
      Fr = "Le multiple de la moyenne des volumes que le volume de la bougie doit atteindre pour déclencher l'alerte. 2 signifie le double de la moyenne.",
      PtBr = "Quantas vezes a média de volume o volume da vela precisa atingir para acionar o alerta. 2 significa o dobro da média.",
      Uk = "У скільки разів обсяг свічки має перевищити середнє, щоб спрацював алерт. 2 - це подвійне перевищення.",
      Be = "У колькі разоў аб'ём свечкі павінен перавысіць сярэдняе, каб спрацавала апавяшчэнне. 2 - гэта падвойнае перавышэнне.")]
  public double Multiplier { get; set; } = 2.0;

  public int GetCandleCount() => Periods + 1;
}
