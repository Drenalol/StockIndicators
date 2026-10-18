using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "A middle band (SMA) and two outer bands placed N standard deviations away. The bands widen in volatile markets and squeeze in quiet ones - a squeeze often precedes a strong move. The alert fires when the price exits the bands or returns inside them.",
    Ru = "Средняя полоса (SMA) и две внешние полосы на расстоянии N стандартных отклонений. Полосы расширяются в волатильном рынке и сжимаются в спокойном - «сжатие» часто предвещает сильный ход. Алерт срабатывает, когда цена выходит за полосы или возвращается внутрь.",
    Es = "Una banda central (SMA) y dos bandas exteriores situadas a N desviaciones estándar. Las bandas se ensanchan en mercados volátiles y se comprimen en los tranquilos - una compresión suele anticipar un movimiento fuerte. La alerta se activa cuando el precio sale de las bandas o vuelve a entrar en ellas.",
    It = "Una banda centrale (SMA) e due bande esterne posizionate a N deviazioni standard. Le bande si allargano nei mercati volatili e si stringono in quelli tranquilli - una compressione spesso precede un movimento forte. L'avviso scatta quando il prezzo esce dalle bande o vi rientra.",
    De = "Ein mittleres Band (SMA) und zwei äußere Bänder im Abstand von N Standardabweichungen. Die Bänder weiten sich in volatilen Märkten und verengen sich in ruhigen - eine Verengung kündigt oft eine starke Bewegung an. Der Alert wird ausgelöst, wenn der Kurs aus den Bändern austritt oder in sie zurückkehrt.",
    Fr = "Une bande médiane (SMA) et deux bandes extérieures placées à N écarts-types. Les bandes s'élargissent sur les marchés volatils et se resserrent sur les marchés calmes - un resserrement précède souvent un mouvement fort. L'alerte se déclenche quand le cours sort des bandes ou y revient.",
    PtBr = "Uma banda central (SMA) e duas bandas externas posicionadas a N desvios padrão. As bandas se alargam em mercados voláteis e se comprimem em mercados calmos - uma compressão costuma preceder um movimento forte. O alerta é acionado quando o preço sai das bandas ou retorna para dentro delas.",
    Uk = "Середня смуга (SMA) і дві зовнішні смуги на відстані N стандартних відхилень. Смуги розширюються на волатильному ринку і звужуються на спокійному - стискання часто передує сильному руху. Алерт спрацьовує, коли ціна виходить за смуги або повертається всередину.",
    Be = "Сярэдняя паласа (SMA) і дзве вонкавыя паласы на адлегласці N стандартных адхіленняў. Паласы пашыраюцца на валятыльным рынку і звужаюцца на спакойным - сцісканне часта папярэднічае моцнаму руху. Апавяшчэнне спрацоўвае, калі цана выходзіць за паласы або вяртаецца ўнутр.")]
public class BollingerBandsIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
      En = "Number of candles for the middle band (SMA). Standard is 20.",
      Ru = "Количество свечей для средней полосы (SMA). Стандарт - 20.",
      Es = "Número de velas para la banda central (SMA). El estándar es 20.",
      It = "Numero di candele per la banda centrale (SMA). Lo standard è 20.",
      De = "Anzahl der Kerzen für das mittlere Band (SMA). Standard ist 20.",
      Fr = "Nombre de bougies pour la bande médiane (SMA). La valeur standard est 20.",
      PtBr = "Número de velas para a banda central (SMA). O padrão é 20.",
      Uk = "Кількість свічок для середньої смуги (SMA). Стандарт - 20.",
      Be = "Колькасць свачак для сярэдняй паласы (SMA). Стандарт - 20.")]
  public int Period { get; set; } = 20;

  [ParameterHint(
      En = "Width of the bands in standard deviations from the middle band. Standard is 2.",
      Ru = "Ширина полос в стандартных отклонениях от средней полосы. Стандарт - 2.",
      Es = "Anchura de las bandas en desviaciones estándar respecto a la banda central. El estándar es 2.",
      It = "Larghezza delle bande in deviazioni standard dalla banda centrale. Lo standard è 2.",
      De = "Breite der Bänder in Standardabweichungen vom mittleren Band. Standard ist 2.",
      Fr = "Largeur des bandes en écarts-types par rapport à la bande médiane. La valeur standard est 2.",
      PtBr = "Largura das bandas em desvios padrão em relação à banda central. O padrão é 2.",
      Uk = "Ширина смуг у стандартних відхиленнях від середньої смуги. Стандарт - 2.",
      Be = "Шырыня палос у стандартных адхіленнях ад сярэдняй паласы. Стандарт - 2.")]
  public double StandardDeviation { get; set; } = 2;

  [ParameterHint(
      En = "Which price of the candle (open, high, low, close) is used in the indicator calculation. The alert fires when the price moves outside or back inside the bands.",
      Ru = "Какая цена свечи (открытие, максимум, минимум, закрытие) используется в расчёте. Алерт срабатывает, когда цена выходит за полосы или возвращается внутрь.",
      Es = "Qué precio de la vela (apertura, máximo, mínimo, cierre) se usa en el cálculo del indicador. La alerta se activa cuando el precio sale de las bandas o vuelve a entrar en ellas.",
      It = "Quale prezzo della candela (apertura, massimo, minimo, chiusura) viene usato nel calcolo dell'indicatore. L'avviso scatta quando il prezzo esce dalle bande o vi rientra.",
      De = "Welcher Kurs der Kerze (Eröffnung, Hoch, Tief, Schluss) bei der Indikatorberechnung verwendet wird. Der Alert wird ausgelöst, wenn der Kurs aus den Bändern austritt oder in sie zurückkehrt.",
      Fr = "Quel prix de la bougie (ouverture, plus haut, plus bas, clôture) est utilisé dans le calcul de l'indicateur. L'alerte se déclenche quand le cours sort des bandes ou y revient.",
      PtBr = "Qual preço da vela (abertura, máxima, mínima, fechamento) é usado no cálculo do indicador. O alerta é acionado quando o preço sai das bandas ou retorna para dentro delas.",
      Uk = "Яка ціна свічки (відкриття, максимум, мінімум, закриття) використовується в розрахунку індикатора. Алерт спрацьовує, коли ціна виходить за смуги або повертається всередину.",
      Be = "Якая цана свічкі (адкрыццё, максімум, мінімум, закрыццё) выкарыстоўваецца ў разліку індыкатара. Апавяшчэнне спрацоўвае, калі цана выходзіць за паласы або вяртаецца ўнутр.")]
  public CandlePart CandlePart { get; set; } = CandlePart.Close;

  [ParameterHint(
      En = "Which band fires the alert: Up - events of the upper band only (breaking out or returning inside), Down - events of the lower band only, Both - both bands.",
      Ru = "Какая полоса даёт алерт: «Рост» - только события верхней полосы (выход за неё или возврат внутрь), «Падение» - только события нижней полосы, «Оба» - обе полосы.",
      Es = "Qué banda activa la alerta: «Subida» - solo eventos de la banda superior (salida o retorno al interior), «Caída» - solo eventos de la banda inferior, «Ambos» - las dos bandas.",
      It = "Quale banda attiva l'avviso: Up - solo gli eventi della banda superiore (uscita o rientro al suo interno), Down - solo gli eventi della banda inferiore, Both - entrambe le bande.",
      De = "Welches Band den Alert auslöst: Up - nur Ereignisse des oberen Bands (Ausbruch oder Rückkehr nach innen), Down - nur Ereignisse des unteren Bands, Both - beide Bänder.",
      Fr = "Quelle bande déclenche l'alerte : Up - uniquement les événements de la bande supérieure (sortie ou retour à l'intérieur), Down - uniquement les événements de la bande inférieure, Both - les deux bandes.",
      PtBr = "Qual banda aciona o alerta: Up - apenas eventos da banda superior (saída ou retorno para dentro), Down - apenas eventos da banda inferior, Both - as duas bandas.",
      Uk = "Яка смуга дає алерт: Up - лише події верхньої смуги (вихід за межі або повернення всередину), Down - лише події нижньої смуги, Both - обидві смуги.",
      Be = "Якая паласа дае апавяшчэнне: Up - толькі падзеі верхняй паласы (выхад за яе або вяртанне ўнутр), Down - толькі падзеі ніжняй паласы, Both - абедзве паласы.")]
  public AlertDirection Direction { get; set; } = AlertDirection.Both;

  public int GetCandleCount() => Period + 10;
}
