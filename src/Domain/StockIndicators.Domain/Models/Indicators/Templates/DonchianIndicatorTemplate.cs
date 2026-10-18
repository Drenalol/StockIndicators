using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "A price channel drawn from the highest high and the lowest low over the lookback window: the classic breakout channel. A close beyond the channel lines marks a new extreme of the window - a breakout move. The alert fires when the close leaves or returns into the channel.",
    Ru = "Ценовой канал по максимуму и минимуму за окно расчёта: классический канал пробоя. Закрытие за линиями канала - новый экстремум окна, пробойное движение. Алерт срабатывает, когда закрытие выходит из канала или возвращается в него.",
    Es = "Un canal de precio trazado desde el máximo más alto y el mínimo más bajo de la ventana de cálculo: el canal de ruptura clásico. Un cierre más allá de las líneas del canal marca un nuevo extremo de la ventana - un movimiento de ruptura. La alerta se activa cuando el cierre sale del canal o vuelve a él.",
    It = "Un canale di prezzo tracciato dal massimo più alto e dal minimo più basso della finestra di lookback: il classico canale di rottura. Una chiusura oltre le linee del canale segna un nuovo estremo della finestra - un movimento di rottura. L'avviso scatta quando la chiusura esce dal canale o vi rientra.",
    De = "Ein Preiskanal, gezeichnet vom höchsten Hoch und dem tiefsten Tief des Rückblickfensters: der klassische Ausbruchskanal. Ein Schlusskurs jenseits der Kanallinien markiert ein neues Extrem des Fensters - eine Ausbruchsbewegung. Der Alert wird ausgelöst, wenn der Schlusskurs den Kanal verlässt oder in ihn zurückkehrt.",
    Fr = "Un canal de prix tracé à partir du plus haut des plus hauts et du plus bas des plus bas de la fenêtre rétrospective : le canal de rupture classique. Une clôture au-delà des lignes du canal marque un nouvel extrême de la fenêtre - un mouvement de rupture. L'alerte se déclenche quand la clôture sort du canal ou y revient.",
    PtBr = "Um canal de preço traçado a partir da máxima mais alta e da mínima mais baixa da janela de retrospecto: o clássico canal de ruptura. Um fechamento além das linhas do canal marca um novo extremo da janela - um movimento de ruptura. O alerta é acionado quando o fechamento sai do canal ou retorna a ele.",
    Uk = "Ціновий канал, побудований за найвищим максимумом і найнижчим мінімумом за вікно ретроспективи: класичний канал пробою. Закриття за лініями каналу позначає новий екстремум вікна - пробійний рух. Алерт спрацьовує, коли закриття виходить із каналу або повертається в нього.",
    Be = "Канал коштаў, пабудаваны па найвышэйшым максікуме і найніжэйшым мінімуме за акна рэтраспекцыі: класічны канал прабоя. Закрыццё за лініямі каналу адзначае новы экстрэмум акна - прабойны рух. Апавяшчэнне спрацоўвае, калі закрыццё выходзіць з каналу або вяртаецца ў яго.")]
public class DonchianIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
      En = "Lookback window in candles whose extremes build the channel. Standard is 20.",
      Ru = "Окно ретроспективы в свечах, чьи экстремумы строят канал. Стандарт - 20.",
      Es = "Ventana retroactiva en velas cuyos extremos construyen el canal. El estándar es 20.",
      It = "Finestra di lookback in candele i cui estremi costruiscono il canale. Lo standard è 20.",
      De = "Rückblickfenster in Kerzen, dessen Extreme den Kanal bilden. Standard ist 20.",
      Fr = "Fenêtre rétrospective en bougies dont les extrêmes forment le canal. La valeur standard est 20.",
      PtBr = "Janela de retrospecto em velas cujos extremos formam o canal. O padrão é 20.",
      Uk = "Вікно ретроспективи у свічках, екстремуми якого формують канал. Стандарт - 20.",
      Be = "Акна рэтраспекцыі ў свачках, экстрэумы якога фарміруюць канал. Стандарт - 20.")]
  public int Period { get; set; } = 20;
  public int GetCandleCount() => Period * 2;
}
