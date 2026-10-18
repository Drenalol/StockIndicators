using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "The volume-weighted average price over a session window. The key intraday level: price above VWAP means buyers are in control, below - sellers. The alert fires when the price crosses VWAP beyond the deviation band.",
    Ru = "Средневзвешенная по объёму цена за сессию. Ключевой уровень для внутридневной торговли: цена выше VWAP - активнее покупатели, ниже - продавцы. Алерт срабатывает, когда цена пересекает VWAP за пределами полосы отклонения.",
    Es = "El precio medio ponderado por volumen en una ventana de sesión. El nivel intradía clave: precio sobre el VWAP significa que mandan los compradores, bajo él - los vendedores. La alerta se activa cuando el precio cruza el VWAP más allá de la banda de desviación.",
    It = "Il prezzo medio ponderato sul volume su una finestra di sessione. Il livello intraday chiave: prezzo sopra il VWAP significa che comandano i compratori, sotto - i venditori. L'avviso scatta quando il prezzo incrocia il VWAP oltre la banda di deviazione.",
    De = "Der volumengewichtete Durchschnittskurs über ein Sitzungsfenster. Das wichtigste Intraday-Level: Kurs über dem VWAP bedeutet, die Käufer bestimmen das Geschehen, darunter - die Verkäufer. Der Alert wird ausgelöst, wenn der Kurs den VWAP außerhalb des Abweichungsbands kreuzt.",
    Fr = "Le cours moyen pondéré par le volume sur une fenêtre de séance. Le niveau intraday clé : un prix au-dessus du VWAP signifie que les acheteurs maîtrisent le marché, en dessous - les vendeurs. L'alerte se déclenche quand le prix croise le VWAP au-delà de la bande d'écart.",
    PtBr = "O preço médio ponderado por volume em uma janela de sessão. O nível intradiário chave: preço acima do VWAP significa que os compradores estão no controle, abaixo - os vendedores. O alerta é acionado quando o preço cruza o VWAP além da banda de desvio.",
    Uk = "Середньозважена за обсягом ціна за сесійне вікно. Ключовий рівень для intraday-торгівлі: ціна вище VWAP - переважають покупці, нижче - продавці. Алерт спрацьовує, коли ціна перетинає VWAP за межею смуги відхилення.",
    Be = "Узважаная па аб'ёме сярэдняя цана за сесійнае акно. Ключавы ўзровень для гандлю на працягу дня: цана вышэй за VWAP - перавага ў пакупнікоў, ніжэй - у прадаўцоў. Апавяшчэнне спрацоўвае, калі цана перасякае VWAP па-за межамі паласы адхілення.")]
public class VwapIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
  public TimeFrame TimeFrame { get; set; } = TimeFrame.Minute;

  [ParameterHint(
      En = "Session length in candles for the VWAP window (e.g. 390 one-minute candles for a US trading day, 1440 for a 24h crypto session).",
      Ru = "Длина сессии в свечах для окна расчёта VWAP (например, 390 минутных свечей для торгового дня US, 1440 для 24-часовой крипто-сессии).",
      Es = "Duración de la sesión en velas para la ventana del VWAP (p. ej. 390 velas de un minuto para un día bursátil de EE. UU., 1440 para una sesión cripto de 24 h).",
      It = "Durata della sessione in candele per la finestra del VWAP (es. 390 candele da un minuto per una giornata di borsa USA, 1440 per una sessione crypto di 24h).",
      De = "Sitzungsdauer in Kerzen für das VWAP-Fenster (z. B. 390 Minutenkerzen für einen US-Handelstag, 1440 für eine 24-Stunden-Krypto-Session).",
      Fr = "Durée de la séance en bougies pour la fenêtre du VWAP (par ex. 390 bougies d'une minute pour une journée de cotation américaine, 1440 pour une séance crypto de 24 h).",
      PtBr = "Duração da sessão em velas para a janela do VWAP (por exemplo, 390 velas de um minuto para um dia de pregão nos EUA, 1440 para uma sessão de cripto de 24h).",
      Uk = "Тривалість сесії у свічках для вікна VWAP (наприклад, 390 хвилинних свічок для торгового дня США, 1440 для 24-годинної крипто-сесії).",
      Be = "Працягласць сесіі ў свечках для акна VWAP (напрыклад, 390 хвілінных свечак для гандлёвага дня ЗША, 1440 для 24-гадзіннай крыпта-сесіі).")]
  public int Period { get; set; } = 390;

  [ParameterHint(
      En = "Deviation threshold as a fraction of VWAP (0.001 = 0.1%). The alert fires when the price crosses VWAP beyond this band.",
      Ru = "Порог отклонения в доле от VWAP (0.001 = 0.1%). Алерт срабатывает, когда цена пересекает VWAP с превышением этого порога.",
      Es = "Umbral de desviación como fracción del VWAP (0.001 = 0.1%). La alerta se activa cuando el precio cruza el VWAP superando esta banda.",
      It = "Soglia di deviazione come frazione del VWAP (0.001 = 0.1%). L'avviso scatta quando il prezzo incrocia il VWAP oltre questa banda.",
      De = "Abweichungsschwelle als Bruchteil des VWAP (0.001 = 0,1 %). Der Alert wird ausgelöst, wenn der Kurs den VWAP außerhalb dieses Bands kreuzt.",
      Fr = "Seuil de déviation en fraction du VWAP (0.001 = 0.1 %). L'alerte se déclenche quand le prix croise le VWAP au-delà de cette bande.",
      PtBr = "Limiar de desvio como fração do VWAP (0.001 = 0.1%). O alerta é acionado quando o preço cruza o VWAP além dessa banda.",
      Uk = "Поріг відхилення як частка VWAP (0.001 = 0.1%). Алерт спрацьовує, коли ціна перетинає VWAP за межею цієї смуги.",
      Be = "Парог адхілення як частка VWAP (0.001 = 0.1%). Апавяшчэнне спрацоўвае, калі цана перасякае VWAP па-за межамі гэтай паласы.")]
  public double Deviation { get; set; } = 0.001;

  public int GetCandleCount() => Period;
}
