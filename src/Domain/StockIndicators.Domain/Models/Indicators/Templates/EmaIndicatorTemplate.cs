using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "Like a simple moving average, but with higher weight on recent candles, so it follows the price more closely and reacts to changes faster. Suited for short-term trends and crossovers. The alert fires when a candle crosses the average within the deviation band.",
    Ru = "Как простая скользящая средняя, но с большим весом последних свечей - плотнее следует за ценой и быстрее реагирует на её изменение. Подходит для краткосрочного тренда и пересечений. Алерт срабатывает, когда свеча пересекает среднюю в пределах полосы допуска.",
    Es = "Igual que una media móvil simple, pero con mayor peso en las velas recientes, por lo que sigue el precio más de cerca y reacciona antes a los cambios. Adecuada para tendencias de corto plazo y cruces. La alerta se activa cuando una vela cruza la media dentro de la banda de tolerancia.",
    It = "Come una media mobile semplice, ma con maggiore peso sulle candele recenti, quindi segue il prezzo più da vicino e reagisce più in fretta ai cambiamenti. Adatta a trend di breve periodo e incroci. L'avviso scatta quando una candela incrocia la media entro la banda di tolleranza.",
    De = "Wie ein einfacher gleitender Durchschnitt, aber mit höherem Gewicht der letzten Kerzen - er folgt dem Kurs enger und reagiert schneller auf Änderungen. Geeignet für kurzfristige Trends und Kreuzungen. Der Alert wird ausgelöst, wenn eine Kerze den Durchschnitt innerhalb der Toleranzbandbreite kreuzt.",
    Fr = "Comme une moyenne mobile simple, mais avec un poids plus élevé sur les bougies récentes : elle suit le prix de plus près et réagit plus vite aux changements. Adaptée aux tendances à court terme et aux croisements. L'alerte se déclenche lorsqu'une bougie croise la moyenne à l'intérieur de la bande de tolérance.",
    PtBr = "Igual a uma média móvel simples, mas com maior peso nas velas recentes, por isso segue o preço mais de perto e reage mais rápido às mudanças. Adequada para tendências de curto prazo e cruzamentos. O alerta é acionado quando uma vela cruza a média dentro da banda de tolerância.",
    Uk = "Як проста ковзна середня, але з більшою вагою останніх свічок - щільніше слідкує за ціною і швидше реагує на зміни. Підходить для короткострокових трендів і перетинів. Алерт спрацьовує, коли свічка перетинає середню в межах смуги допуску.",
    Be = "Як простая слізгальная сярэдняя, але з большай вагой апошніх свічак - шчылей ідзе за цаной і хутчэй рэагуе на яе змены. Падыходзіць для кароткатэрміновых трэндаў і перасячэнняў. Апавяшчэнне спрацоўвае, калі свечка перасякае сярэднюю ў межах палы допуску.")]
public class EmaIndicatorTemplate : MaIndicatorTemplate
{
  public override int GetCandleCount() => Periods * 3;
}