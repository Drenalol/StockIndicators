using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "A trailing stop line: the extreme of the lookback window moved aside by an ATR multiple. For a long position it hangs below the price (highest high minus ATR), for a short one above it (lowest low plus ATR). The alert fires when the close crosses the stop line, i.e. the position should exit.",
    Ru = "Стоп-линия, следующая за ценой: экстремум окна расчёта, отодвинутый на множитель ATR. Для длинной позиции висит под ценой (максимум минус ATR), для короткой - над ней (минимум плюс ATR). Алерт срабатывает, когда закрытие пересекает стоп-линию, то есть позицию пора закрывать.",
    Es = "Una línea de stop móvil: el extremo de la ventana de cálculo desplazado por un múltiplo del ATR. Para una posición larga cuelga bajo el precio (máximo menos ATR); para una corta, sobre él (mínimo más ATR). La alerta se activa cuando el cierre cruza la línea de stop, es decir, la posición debe salir.",
    It = "Una linea di trailing stop: l'estremo della finestra di lookback spostato di un multiplo dell'ATR. Per una posizione lunga pende sotto il prezzo (il massimo più alto meno ATR), per una corta sta sopra (il minimo più basso più ATR). L'avviso scatta quando la chiusura incrocia la linea di stop, cioè la posizione andrebbe chiusa.",
    De = "Eine Trailing-Stop-Linie: das Extrem des Rückblickfensters, versetzt um ein Vielfaches des ATR. Bei einer Long-Position hängt sie unter dem Kurs (höchstes Hoch minus ATR), bei einer Short-Position darüber (niedrigstes Tief plus ATR). Der Alert wird ausgelöst, wenn der Schlusskurs die Stop-Linie kreuzt, d. h. die Position sollte geschlossen werden.",
    Fr = "Une ligne de trailing stop : l'extrême de la fenêtre rétrospective décalé d'un multiple de l'ATR. Pour une position longue, elle se situe sous le cours (plus haut des plus hauts moins l'ATR), pour une courte au-dessus (plus bas des plus bas plus l'ATR). L'alerte se déclenche quand la clôture croise la ligne de stop, c'est-à-dire que la position devrait être soldée.",
    PtBr = "Uma linha de trailing stop: o extremo da janela de retrospecto deslocado por um múltiplo do ATR. Para uma posição comprada, fica abaixo do preço (a mais alta das máximas menos o ATR); para uma vendida, acima (a mais baixa das mínimas mais o ATR). O alerta é acionado quando o fechamento cruza a linha de stop, ou seja, a posição deve ser encerrada.",
    Uk = "Лінія трейлінг-стопу: екстремум вікна ретроспективи, відсунутий на множник ATR. Для довгої позиції він висить під ціною (найвищий максимум мінус ATR), для короткої - над нею (найнижчий мінімум плюс ATR). Алерт спрацьовує, коли закриття перетинає лінію стопу, тобто позицію слід закрити.",
    Be = "Лінія трэйл-стопу: экстрэмум акна рэтраспекцыі, адсунуты на множнік ATR. Для доўгай пазіцыі ён вісіць пад цаной (найвышэйшы максімум мінус ATR), для кароткай - над ёй (найніжэйшы мінімум плюс ATR). Апавяшчэнне спрацоўвае, калі закрыццё перасякае лінію стопу, г.зн. пазіцыю трэба закрываць.")]
public class ChandelierIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
      En = "Lookback window in candles whose extreme the stop line is built from. Standard is 22.",
      Ru = "Окно ретроспективы в свечах, чей экстремум служит основой стоп-линии. Стандарт - 22.",
      Es = "Ventana retroactiva en velas cuyo extremo sirve de base a la línea de stop. El estándar es 22.",
      It = "Finestra di lookback in candele il cui estremo costituisce la base della linea di stop. Lo standard è 22.",
      De = "Rückblickfenster in Kerzen, dessen Extrem der Stop-Linie zugrunde liegt. Standard ist 22.",
      Fr = "Fenêtre rétrospective en bougies dont l'extrême sert de base à la ligne de stop. La valeur standard est 22.",
      PtBr = "Janela de retrospecto em velas cujo extremo serve de base para a linha de stop. O padrão é 22.",
      Uk = "Вікно ретроспективи у свічках, екстремум якого є основою лінії стопу. Стандарт - 22.",
      Be = "Акна рэтраспекцыі ў свачках, экстрэмум якога служыць асновай лініі стопу. Стандарт - 22.")]
  public int Period { get; set; } = 22;

  [ParameterHint(
      En = "ATR multiplier - the distance the stop line keeps from the window extreme. A larger value means a looser stop and rarer alerts. Standard is 3.",
      Ru = "Множитель ATR - расстояние, которое стоп-линия держит от экстремума окна. Чем больше значение, тем свободнее стоп и реже алерты. Стандарт - 3.",
      Es = "Múltiplo del ATR: la distancia que la línea de stop mantiene desde el extremo de la ventana. Un valor mayor significa un stop más holgado y alertas más raras. El estándar es 3.",
      It = "Multiplo dell'ATR - la distanza che la linea di stop mantiene dall'estremo della finestra. Un valore più grande significa uno stop più largo e avvisi più rari. Lo standard è 3.",
      De = "ATR-Multiplikator - der Abstand, den die Stop-Linie vom Extrem des Fensters hält. Ein größerer Wert bedeutet einen lockereren Stop und seltenere Alerts. Standard ist 3.",
      Fr = "Multiple de l'ATR - la distance que la ligne de stop maintient par rapport à l'extrême de la fenêtre. Une valeur plus élevée signifie un stop plus large et des alertes plus rares. La valeur standard est 3.",
      PtBr = "Múltiplo do ATR - a distância que a linha de stop mantém em relação ao extremo da janela. Um valor maior significa um stop mais folgado e alertas mais raros. O padrão é 3.",
      Uk = "Множник ATR - відстань, яку лінія стопу тримає від екстремуму вікна. Більше значення означає вільніший стоп і більш рідкісні алерти. Стандарт - 3.",
      Be = "Множнік ATR - адлегласць, якую лінія стопу трымае ад экстрэмуму акна. Большая велічыня азначае больш свабодны стоп і больш рэдкія апавяшчэнні. Стандарт - 3.")]
  public double Multiplier { get; set; } = 3.0;

  [ParameterHint(
      En = "Which stop to track: Long - the line below price (highest high minus ATR), Short - the line above price (lowest low plus ATR).",
      Ru = "Какый стоп отслеживать: Long - линия под ценой (максимум минус ATR), Short - линия над ценой (минимум плюс ATR).",
      Es = "Qué stop seguir: Long - la línea bajo el precio (máximo menos ATR), Short - la línea sobre el precio (mínimo más ATR).",
      It = "Quale stop seguire: Long - la linea sotto il prezzo (il massimo più alto meno ATR), Short - la linea sopra il prezzo (il minimo più basso più ATR).",
      De = "Welchem Stop folgen: Long - die Linie unter dem Kurs (höchstes Hoch minus ATR), Short - die Linie über dem Kurs (niedrigstes Tief plus ATR).",
      Fr = "Quel stop suivre : Long - la ligne sous le cours (plus haut des plus hauts moins l'ATR), Short - la ligne au-dessus du cours (plus bas des plus bas plus l'ATR).",
      PtBr = "Qual stop acompanhar: Long - a linha abaixo do preço (a mais alta das máximas menos o ATR), Short - a linha acima do preço (a mais baixa das mínimas mais o ATR).",
      Uk = "Який стоп відстежувати: Long - лінія під ціною (найвищий максимум мінус ATR), Short - лінія над ціною (найнижчий мінімум плюс ATR).",
      Be = "За якім стопам сачыць: Long - лінія пад цаной (найвышэйшы максімум мінус ATR), Short - лінія над цаной (найніжэйшы мінімум плюс ATR).")]
  public ChandelierDirection Type { get; set; } = ChandelierDirection.Long;
  public int GetCandleCount() => Period * 2;
}
