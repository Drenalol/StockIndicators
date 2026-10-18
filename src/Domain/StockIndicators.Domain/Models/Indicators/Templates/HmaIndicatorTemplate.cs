using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "Hull Moving Average: a combination of weighted averages of different lengths that removes most of the lag while keeping the line smooth. It hugs price turns closely, so a cross of it marks an early turn. The alert fires when a candle crosses the average within the deviation band.",
    Ru = "Скользящая средняя Холла: комбинация взвешенных средних разной длины, убирающая большую часть лага при сохранении гладкости. Плотно обнимает развороты цены, поэтому её пересечение отмечает ранний разворот. Алерт срабатывает, когда свеча пересекает среднюю в пределах полосы допуска.",
    Es = "La media móvil de Hull: una combinación de medias ponderadas de distintas longitudes que elimina gran parte del retardo manteniendo la línea suave. Se ajusta a los giros del precio, por lo que su cruce marca un giro temprano. La alerta se activa cuando una vela cruza la media dentro de la banda de tolerancia.",
    It = "Media mobile di Hull: una combinazione di medie ponderate di lunghezze diverse che elimina gran parte del ritardo mantenendo la linea fluida. Segue da vicino le svolte del prezzo, quindi il suo incrocio segna una svolta anticipata. L'avviso scatta quando una candela incrocia la media entro la banda di tolleranza.",
    De = "Hull-Gleitender Durchschnitt: eine Kombination gewichteter Durchschnitte unterschiedlicher Länge, die den größten Teil des Nachlaufs beseitigt und die Linie glatt hält. Sie folgt den Preiswendungen eng, daher markiert ihre Kreuzung eine frühe Wendung. Der Alert wird ausgelöst, wenn eine Kerze den Durchschnitt innerhalb der Toleranzbandbreite kreuzt.",
    Fr = "Moyenne mobile de Hull : combinaison de moyennes pondérées de longueurs différentes qui élimine l'essentiel du retard tout en gardant la ligne fluide. Elle épouse les retournements du prix, si bien que son croisement signale un retournement précoce. L'alerte se déclenche lorsqu'une bougie croise la moyenne à l'intérieur de la bande de tolérance.",
    PtBr = "Média móvel de Hull: uma combinação de médias ponderadas de diferentes comprimentos que elimina a maior parte do atraso mantendo a linha suave. Ela acompanha de perto as viradas do preço, então seu cruzamento marca uma virada antecipada. O alerta é acionado quando uma vela cruza a média dentro da banda de tolerância.",
    Uk = "Ковзна середня Холла: комбінація зважених середніх різної довжини, що прибирає більшу частину лагу, залишаючи лінію гладкою. Вона щільно огортає розвороти ціни, тому її перетин позначає ранній розворот. Алерт спрацьовує, коли свічка перетинає середню в межах смуги допуску.",
    Be = "Слізгальная сярэдняя Хала: камбінацыя ўзважаных сярэдніх рознай даўжыні, якая прыбірае большую частку затрымкі, захоўваючы лінію гладкай. Яна шчыльна ахоплівае развароты цэны, таму яе перасячэнне адзначае раны разварот. Апавяшчэнне спрацоўвае, калі свечка перасякае сярэднюю ў межах палы допуску.")]
public class HmaIndicatorTemplate : MaIndicatorTemplate
{
  public override int GetCandleCount() => Periods * 2;
}
