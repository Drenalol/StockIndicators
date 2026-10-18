using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "A moving average whose weights grow linearly toward the last candle, so it reacts to price faster than a simple average while staying smooth. The alert fires when a candle opens on one side of the average and closes on the other side within the deviation band.",
    Ru = "Скользящая средняя с линейно растущими весами последних свечей: реагирует на цену быстрее простой средней, оставаясь гладкой. Алерт срабатывает, когда свеча открывается с одной стороны средней и закрывается с другой в пределах полосы допуска.",
    Es = "Una media móvil cuyos pesos crecen linealmente hacia la última vela, por lo que reacciona al precio más rápido que la simple manteniéndose suave. La alerta se activa cuando una vela abre a un lado de la media y cierra al otro dentro de la banda de tolerancia.",
    It = "Una media mobile i cui pesi crescono linearmente verso l'ultima candela, quindi reagisce al prezzo più velocemente di una media semplice restando fluida. L'avviso scatta quando una candela apre da un lato della media e chiude dall'altro entro la banda di deviazione.",
    De = "Ein gleitender Durchschnitt, dessen Gewichte sich zur letzten Kerze hin linear erhöhen, daher reagiert er schneller auf den Kurs als eine einfache, bleibt dabei aber glatt. Der Alert wird ausgelöst, wenn eine Kerze auf der einen Seite des Durchschnitts eröffnet und auf der anderen innerhalb des Abweichungsbands schließt.",
    Fr = "Une moyenne mobile dont les pondérations croissent linéairement vers la dernière bougie : elle réagit donc plus vite au prix qu'une moyenne simple tout en restant fluide. L'alerte se déclenche quand une bougie ouvre d'un côté de la moyenne et clôture de l'autre à l'intérieur de la bande d'écart.",
    PtBr = "Uma média móvel cujos pesos crescem linearmente em direção à última vela, então ela reage ao preço mais rápido que uma média simples, mas continua suave. O alerta é acionado quando uma vela abre de um lado da média e fecha do outro dentro da banda de desvio.",
    Uk = "Ковзна середня, ваги якої лінійно зростаюць до останньої свічки, тож вона реагує на ціну швидше за просту середню, залишаючись гладкою. Алерт спрацьовує, коли свічка відкривається з одного боку середньої і закривається з іншого в межах смуги відхилення.",
    Be = "Слізгальная сярэдняя, вагі якой лінейна павялічваюцца да апошняй свечкі, таму яна рэагуе на цану хутчэй за простую сярэднюю, заставаючыся гладкай. Апавяшчэнне спрацоўвае, калі свечка адкрываецца з аднаго боку сярэдняй і закрываецца з другога ў межах паласы адхілення.")]
public class WmaIndicatorTemplate : MaIndicatorTemplate
{

  public override int GetCandleCount() => Periods;
}
