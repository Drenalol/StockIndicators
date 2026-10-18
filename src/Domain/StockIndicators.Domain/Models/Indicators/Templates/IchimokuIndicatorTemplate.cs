using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "The Ichimoku Kinko Hyo system: five lines forming a cloud (senkou spans) that marks the balance of buyers and sellers ahead in time. Cloud above the price - an uptrend, below - a downtrend, price inside - balance. The alert fires on a Tenkan/Kijun cross.",
    Ru = "Система «Ишимок Кинко Хё»: пять линий, образующих облако (сэнкоу спаны), которое показывает баланс покупателей и продавцов наперёд. Облако выше цены - восходящий тренд, ниже - нисходящий, внутри - равновесие. Алерт срабатывает при пересечении Тенкан и Кицун.",
    Es = "El sistema Ichimoku Kinko Hyo: cinco líneas que forman una nube (span senkou) que marca el equilibrio entre compradores y vendedores por adelantado. Nube sobre el precio - tendencia alcista; bajo el precio - bajista; precio dentro - equilibrio. La alerta se activa con un cruce Tenkan/Kijun.",
    It = "Il sistema Ichimoku Kinko Hyo: cinque linee che formano una nuvola (senkou span) la quale indica in anticipo l'equilibrio tra compratori e venditori. Nuvola sopra il prezzo - trend rialzista, sotto - ribassista, prezzo all'interno - equilibrio. L'avviso scatta su un incrocio Tenkan/Kijun.",
    De = "Das Ichimoku-Kinko-Hyo-System: fünf Linien, die eine Wolke (Senkou-Spans) bilden und das Gleichgewicht von Käufern und Verkäufern vorauszeichnen. Wolke über dem Kurs - Aufwärtstrend, darunter - Abwärtstrend, Kurs darin - Gleichgewicht. Der Alert wird bei einer Tenkan/Kijun-Kreuzung ausgelöst.",
    Fr = "Le système Ichimoku Kinko Hyo : cinq lignes formant un nuage (senkou spans) qui annonce à l'avance l'équilibre entre acheteurs et vendeurs. Nuage au-dessus du prix - tendance haussière, en dessous - baissière, prix dedans - équilibre. L'alerte se déclenche sur un croisement Tenkan/Kijun.",
    PtBr = "O sistema Ichimoku Kinko Hyo: cinco linhas que formam uma nuvem (senkou spans) que antecipa o equilíbrio entre compradores e vendedores. Nuvem acima do preço - tendência de alta, abaixo - de baixa, preço dentro - equilíbrio. O alerta é acionado em um cruzamento Tenkan/Kijun.",
    Uk = "Система Ішімоку Кінко Хьо: п'ять ліній, що утворюють хмару (сенкоу спани), яка показує баланс покупців і продавців наперед. Хмара вище за ціну - висхідний тренд, нижче - спадний, ціна всередині - рівновага. Алерт спрацьовує на перетині Тенкан/Кідзюн.",
    Be = "Сістэма Ішімоку Кінка Хё: пяць ліній, якія ўтвараюць воблака (сенкоу спаны), якое паказвае баланс пакупнікоў і прадаўцоў наперад. Воблака вышэй за цэну - узыходны трэнд, ніжэй - низхадны, цэна ўнутры - роўнавага. Апавяшчэнне спрацоўвае пры перасячэнні Тенкан/Кідзюн.")]
public class IchimokuIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
{
  [ParameterHint(
      En = "Candle timeframe the indicator is calculated on. The alert is evaluated on each closed candle of this period.",
      Ru = "Таймфрейм свечей, по которым считается индикатор. Проверка выполняется при закрытии каждой свечи этого периода.",
      Es = "Temporalidad de las velas con la que se calcula el indicador. La alerta se evalúa al cierre de cada vela de este periodo.",
      It = "Timeframe delle candele su cui viene calcolato l'indicatore. L'avviso viene valutato alla chiusura di ogni candela di questo periodo.",
      De = "Zeitrahmen der Kerzen, auf dem der Indikator berechnet wird. Der Alert wird beim Schluss jeder Kerze dieses Zeitraums ausgewertet.",
      Fr = "Unité de temps des bougies sur laquelle l'indicateur est calculé. L'alerte est évaluée à la clôture de chaque bougie de cette période.",
      PtBr = "Timeframe das velas usado para calcular o indicador. O alerta é avaliado no fechamento de cada vela desse período.",
      Uk = "Таймфрейм свічок, на якому обчислюється індикатор. Алерт перевіряється на кожній закритій свічці цього періоду.",
      Be = "Таймфрейм свічак, на якім вылічваецца індыкатар. Апавяшчэнне правяраецца на кожнай закрытай свічцы гэтага перыяду.")]
  public TimeFrame TimeFrame { get; set; }

  [ParameterHint(
      En = "Period of the Tenkan-sen (fast line). Standard is 9.",
      Ru = "Период линии Тенкан-сен (быстрая линия). Стандарт - 9.",
      Es = "Periodo del Tenkan-sen (línea rápida). El estándar es 9.",
      It = "Periodo del Tenkan-sen (linea veloce). Lo standard è 9.",
      De = "Periode des Tenkan-sen (schnelle Linie). Standard ist 9.",
      Fr = "Période du Tenkan-sen (ligne rapide). Le standard est 9.",
      PtBr = "Período do Tenkan-sen (linha rápida). O padrão é 9.",
      Uk = "Період лінії Тенкан-сен (швидка лінія). Стандарт - 9.",
      Be = "Перыяд лініі Тенкан-сен (хуткая лінія). Стандарт - 9.")]
  public int TenkanPeriod { get; set; } = 9;

  [ParameterHint(
      En = "Period of the Kijun-sen (base line). Standard is 26. The alert fires when Tenkan-sen crosses Kijun-sen.",
      Ru = "Период линии Кидзун-сен (базовая линия). Стандарт - 26. Алерт срабатывает при пересечении Тенкан-сен и Кидзун-сен.",
      Es = "Periodo del Kijun-sen (línea base). El estándar es 26. La alerta se activa cuando el Tenkan-sen cruza el Kijun-sen.",
      It = "Periodo del Kijun-sen (linea di base). Lo standard è 26. L'avviso scatta quando il Tenkan-sen incrocia il Kijun-sen.",
      De = "Periode des Kijun-sen (Baselinie). Standard ist 26. Der Alert wird ausgelöst, wenn der Tenkan-sen den Kijun-sen kreuzt.",
      Fr = "Période du Kijun-sen (ligne de base). Le standard est 26. L'alerte se déclenche lorsque le Tenkan-sen croise le Kijun-sen.",
      PtBr = "Período do Kijun-sen (linha base). O padrão é 26. O alerta é acionado quando o Tenkan-sen cruza o Kijun-sen.",
      Uk = "Період лінії Кідзюн-сен (базова лінія). Стандарт - 26. Алерт спрацьовує, коли Тенкан-сен перетинає Кідзюн-сен.",
      Be = "Перыяд лініі Кідзюн-сен (базавая лінія). Стандарт - 26. Апавяшчэнне спрацоўвае, калі Тенкан-сен перасякае Кідзюн-сен.")]
  public int KijunPeriod { get; set; } = 26;

  [ParameterHint(
      En = "Period of the Senkou Span B (the second cloud line). Standard is 52.",
      Ru = "Период линии Сенкоу Спан B (вторая линия облака). Стандарт - 52.",
      Es = "Periodo del Senkou Span B (la segunda línea de la nube). El estándar es 52.",
      It = "Periodo dello Senkou Span B (la seconda linea della nuvola). Lo standard è 52.",
      De = "Periode des Senkou Span B (die zweite Wolkenlinie). Standard ist 52.",
      Fr = "Période du Senkou Span B (la deuxième ligne du nuage). Le standard est 52.",
      PtBr = "Período do Senkou Span B (a segunda linha da nuvem). O padrão é 52.",
      Uk = "Період Сенкоу Спан B (друга лінія хмари). Стандарт - 52.",
      Be = "Перыяд Сенкоу Спан B (другая лінія воблака). Стандарт - 52.")]
  public int SenkouBPeriod { get; set; } = 52;

  public int GetCandleCount() => SenkouBPeriod + 30;
}
