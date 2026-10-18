using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "Two 0-100 lines that count how long ago the window's high (Aroon Up) and low (Aroon Down) were set: the closer a line is to 100, the more recent the extreme. A fresh bullish trend pushes Aroon Up above Aroon Down. The alert fires when the two lines cross.",
    Ru = "Две линии от 0 до 100, показывающие, как давно были поставлены максимум (Aroon Up) и минимум (Aroon Down) окна: чем ближе линия к 100, тем свежее экстремум. Новый восходящий тренд выводит Aroon Up выше Aroon Down. Алерт срабатывает при пересечении двух линий.",
    Es = "Dos líneas de 0 a 100 que cuentan cuánto tiempo atrás se marcaron el máximo (Aroon Up) y el mínimo (Aroon Down) de la ventana: cuanto más cerca de 100, más reciente el extremo. Una tendencia alcista reciente sube Aroon Up por encima de Aroon Down. La alerta se activa cuando las dos líneas se cruzan.",
    It = "Due linee da 0 a 100 che contano quanto tempo fa sono stati fissati il massimo (Aroon Up) e il minimo (Aroon Down) della finestra: più una linea è vicina a 100, più recente è l'estremo. Un nuovo trend rialzista spinge Aroon Up sopra Aroon Down. L'avviso scatta quando le due linee si incrociano.",
    De = "Zwei Linien von 0 bis 100, die zählen, wie lange das Hoch (Aroon Up) und das Tief (Aroon Down) des Fensters zurückliegen: je näher eine Linie der 100 ist, desto frischer das Extrem. Ein neuer bullischer Trend schiebt Aroon Up über Aroon Down. Der Alert wird ausgelöst, wenn sich die beiden Linien kreuzen.",
    Fr = "Deux lignes de 0 à 100 qui comptent depuis combien de temps le plus haut (Aroon Up) et le plus bas (Aroon Down) de la fenêtre ont été enregistrés : plus une ligne est proche de 100, plus l'extrême est récent. Une nouvelle tendance haussière pousse Aroon Up au-dessus d'Aroon Down. L'alerte se déclenche quand les deux lignes se croisent.",
    PtBr = "Duas linhas de 0 a 100 que contam quanto tempo atrás foram definidos a máxima (Aroon Up) e a mínima (Aroon Down) da janela: quanto mais perto de 100, mais recente o extremo. Uma nova tendência altista empurra o Aroon Up acima do Aroon Down. O alerta é acionado quando as duas linhas se cruzam.",
    Uk = "Дві лінії від 0 до 100, які рахують, як давно були встановлені максимум (Aroon Up) і мінімум (Aroon Down) вікна: чим ближче лінія до 100, тим свіжіший екстремум. Новий бичачий тренд виводить Aroon Up вище за Aroon Down. Алерт спрацьовує, коли дві лінії перетинаються.",
    Be = "Дзве лініі ад 0 да 100, якія лічаць, як даўно былі ўстаноўлены максімум (Aroon Up) і мінімум (Aroon Down) акна: чым бліжэй лінія да 100, тым свежэй экстрэмум. Новы бічыны трэнд выводзіць Aroon Up вышэй за Aroon Down. Апавяшчэнне спрацоўвае, калі дзве лініі перасякаюцца.")]
public class AroonIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
      En = "Lookback window in candles used to find the window high and low. Standard is 25.",
      Ru = "Окно ретроспективы в свечах для поиска максимума и минимума. Стандарт - 25.",
      Es = "Ventana retroactiva en velas para encontrar el máximo y el mínimo. El estándar es 25.",
      It = "Finestra di lookback in candele usata per trovare il massimo e il minimo della finestra. Lo standard è 25.",
      De = "Rückblickfenster in Kerzen, in dem das Hoch und das Tief gesucht werden. Standard ist 25.",
      Fr = "Fenêtre rétrospective en bougies utilisée pour trouver le plus haut et le plus bas de la fenêtre. La valeur standard est 25.",
      PtBr = "Janela de retrospecto em velas usada para encontrar a máxima e a mínima do período. O padrão é 25.",
      Uk = "Вікно ретроспективи у свічках для пошуку максимуму та мінімуму. Стандарт - 25.",
      Be = "Акна рэтраспекцыі ў свачках для пошуку максімуму і мінімуму. Стандарт - 25.")]
  public int Period { get; set; } = 25;
  public int GetCandleCount() => Period * 2;
}
