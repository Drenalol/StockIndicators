using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "A trailing-stop alert: the threshold does not sit at a fixed price but follows the best price reached since the alert was set. For a long position the stop hangs under the highest high since arming (by a percent of it or a fixed amount); the alert fires when the close drops through that stop line - time to exit. For a short one the stop sits above the lowest low and the alert fires when the close rises through it. It re-arms once the close crosses back to the safe side.",
    Ru = "Трейлинг-стоп: порог висит не на фиксированной цене, а едет за лучшей ценой с момента постановки алерта. Для лонга стоп висит под максимумом с момента взведения (на процент от него или на фиксированную величину); алерт срабатывает, когда закрытие падает сквозь эту стоп-линию - пора выходить из позиции. Для шорта стоп висит над минимумом, и алерт срабатывает при подъёме закрытия сквозь него. Повторное взведение происходит, когда закрытие возвращается на безопасную сторону.",
    Es = "Una alerta de stop dinámico: el umbral no está en un precio fijo sino que sigue al mejor precio alcanzado desde que se activó la alerta. Para una posición larga el stop cuelga bajo el máximo desde el rearme (un porcentaje de él o una cantidad fija); la alerta se activa cuando el cierre cae a través de esa línea de stop: es hora de salir. Para una corta el stop está sobre el mínimo y la alerta salta cuando el cierre la cruza hacia arriba. Se rearma cuando el cierre vuelve al lado seguro.",
    It = "Un avviso con trailing stop: la soglia non è a un prezzo fisso ma segue il miglior prezzo raggiunto da quando l'avviso è stato impostato. Per una posizione lunga lo stop resta sotto il massimo raggiunto dall'attivazione (di una percentuale di quel massimo o di un importo fisso); l'avviso scatta quando la chiusura scende sotto la linea di stop - è il momento di uscire. Per una posizione corta lo stop sta sopra il minimo e l'avviso scatta quando la chiusura risale sopra di esso. Si riarma quando la chiusura torna sul lato sicuro.",
    De = "Ein Trailing-Stop-Alert: Der Schwellenwert liegt nicht auf einem festen Kurs, sondern folgt dem besten Kurs, der seit dem Setzen des Alerts erreicht wurde. Bei einer Long-Position hängt der Stop unter dem höchsten Hoch seit der Aktivierung (um einen Prozentsatz davon oder einen festen Betrag); der Alert wird ausgelöst, wenn der Schlusskurs unter diese Stop-Linie fällt - Zeit für den Ausstieg. Bei einer Short-Position liegt der Stop über dem tiefsten Tief, und der Alert wird ausgelöst, wenn der Schlusskurs über ihn steigt. Er re-aktiviert sich, sobald der Schlusskurs zurück auf die sichere Seite wechselt.",
    Fr = "Une alerte de suivi de cours (trailing stop) : le seuil ne se situe pas à un prix fixe mais suit le meilleur prix atteint depuis la définition de l'alerte. Pour une position longue, le stop est accroché sous le plus haut atteint depuis l'activation (d'un pourcentage de ce niveau ou d'un montant fixe) ; l'alerte se déclenche quand la clôture passe sous cette ligne de stop - il est temps de sortir. Pour une position courte, le stop est au-dessus du plus bas et l'alerte se déclenche quand la clôture remonte au-dessus. Elle se réarme dès que la clôture repasse du côté sûr.",
    PtBr = "Um alerta de trailing stop: o limite não fica em um preço fixo, mas acompanha o melhor preço atingido desde que o alerta foi criado. Para uma posição comprada, o stop fica abaixo da máxima atingida desde a ativação (em uma porcentagem dela ou um valor fixo); o alerta é acionado quando o fechamento cai abaixo dessa linha de stop - hora de sair. Para uma posição vendida, o stop fica acima da mínima e o alerta é acionado quando o fechamento sobe acima dela. Ele rearma quando o fechamento volta para o lado seguro.",
    Uk = "Алерт із трейлінг-стопом: поріг висить не на фіксованій ціні, а рухається за найкращою ціною, досягнутою з моменту встановлення алерту. Для лонгової позиції стоп висить під найвищим максимумом з моменту активації (на відсоток від нього або на фіксовану величину); алерт спрацьовує, коли закриття падає нижче за цю стоп-лінію - час виходити. Для шортової позиції стоп висить над найнижчим мінімумом, і алерт спрацьовує, коли закриття піднімається вище за нього. Повторна активація відбувається, коли закриття повертається на безпечний бік.",
    Be = "Апавяшчэнне з трэйл-стопам: парог вісіць не на фіксаванай цане, а ідзе за найлепшай цаной, дасягненай з моманту ўсталявання апавяшчэння. Для доўгай пазіцыі стоп вісіць пад найвышэйшым максімумам з моманту ўзвядзення (на адсотак ад яго або на фіксаваную велічыню); апавяшчэнне спрацоўвае, калі закрыццё апускаецца ніжэй за гэту стоп-лінію - час выходзіць. Для кароткай пазіцыі стоп вісіць над найніжэйшым мінімумам, і апавяшчэнне спрацоўвае, калі закрыццё падымаецца вышэй за яго. Паўторнае ўзвядзенне адбываецца, калі закрыццё вяртаецца на бяспечны бок.")]
public class TrailingStopIndicatorTemplate : ITimeFrameDependentIndicatorTemplate
{
  [ParameterHint(
      En = "Candle timeframe the stop is checked on. Daily candles trail on daily closes.",
      Ru = "Таймфрейм свечей, на котором проверяется стоп. На дневных свечах стоп идёт по дневным закрытиям.",
      Es = "Temporalidad de las velas en la que se comprueba el stop. Con velas diarias el stop sigue los cierres diarios.",
      It = "Timeframe delle candele su cui viene controllato lo stop. Con candele giornaliere lo stop segue le chiusure giornaliere.",
      De = "Zeitrahmen der Kerzen, auf dem der Stop geprüft wird. Bei Tageskerzen folgt der Stop den Tagesschlusskursen.",
      Fr = "Unité de temps des bougies sur laquelle le stop est vérifié. Sur des bougies journalières, le stop suit les clôtures journalières.",
      PtBr = "Timeframe das velas em que o stop é verificado. Em velas diárias, o stop acompanha os fechamentos diários.",
      Uk = "Таймфрейм свічок, на якому перевіряється стоп. На денних свічках стоп іде за денними закриттями.",
      Be = "Таймфрэйм свічак, на якім правяраецца стоп. На днявых свечках стоп ідзе за днявымі закрыццямі.")]
  public TimeFrame TimeFrame { get; set; } = TimeFrame.Day;

  [ParameterHint(
      En = "Position side to protect: Long - stop under the highest high (fires on a drop-through), Short - stop above the lowest low (fires on a rise-through).",
      Ru = "Защищаемая сторона: «Лонг» - стоп под максимумом (срабатывает при падении сквозь него), «Шорт» - стоп над минимумом (срабатывает при подъёме сквозь него).",
      Es = "Lado de la posición a proteger: Largo - stop bajo el máximo (dispara al caer a través); Corto - stop sobre el mínimo (dispara al subir a través).",
      It = "Lato della posizione da proteggere: Long - lo stop resta sotto il massimo più alto (scatta quando la chiusura lo rompe verso il basso), Short - lo stop sopra il minimo più basso (scatta quando la chiusura lo rompe verso l'alto).",
      De = "Zu schützende Seite der Position: Long - Stop unter dem höchsten Hoch (Auslösung bei einem Fall darunter), Short - Stop über dem tiefsten Tief (Auslösung bei einem Anstieg darüber).",
      Fr = "Côté de la position à protéger : Long - stop sous le plus haut atteint (déclenchement quand la clôture le casse par le bas), Short - stop au-dessus du plus bas atteint (déclenchement quand la clôture le casse par le haut).",
      PtBr = "Lado da posição a proteger: Long - stop abaixo da máxima atingida (aciona quando o fechamento cai abaixo dela), Short - stop acima da mínima atingida (aciona quando o fechamento sobe acima dela).",
      Uk = "Сторона позиції, яку захищаємо: Long - стоп під найвищим максимумом (спрацьовує при падінні нижче за нього), Short - стоп над найнижчим мінімумом (спрацьовує при піднятті вище за нього).",
      Be = "Бок пазіцыі, які абараняем: Long - стоп пад найвышэйшым максімумам (спрацоўвае пры падзенні ніжэй за яго), Short - стоп над найніжэйшым мінімумам (спрацоўвае пры росте вышэй за яго).")]
  public ChandelierDirection Type { get; set; } = ChandelierDirection.Long;

  [ParameterHint(
      En = "How the stop distance is measured: Percent - of the best price reached since arming (5 means the stop stays 5% below a long's high), Absolute - a fixed price amount.",
      Ru = "Как измеряется расстояние стопа: «Процент» - от лучшей цены с момента взведения (5 = стоп держится на 5% ниже максимума лонга), «Абсолют» - фиксированная ценовая величина.",
      Es = "Cómo se mide la distancia del stop: Porcentaje - del mejor precio alcanzado desde el rearme (5 = el stop se mantiene un 5% bajo el máximo del largo), Absoluto - una cantidad de precio fija.",
      It = "Come viene misurata la distanza dello stop: Percent - percentuale del miglior prezzo raggiunto dall'attivazione (5 significa che lo stop resta al 5% sotto il massimo del long), Absolute - un importo di prezzo fisso.",
      De = "Wie der Stop-Abstand gemessen wird: Percent - in Prozent des besten Kurses seit der Aktivierung (5 bedeutet, der Stop bleibt 5 % unter dem Hoch des Longs), Absolute - ein fester Kursbetrag.",
      Fr = "Mode de mesure de la distance du stop : Percent - en pourcentage du meilleur prix atteint depuis l'activation (5 signifie que le stop reste à 5 % sous le plus haut du long), Absolute - un montant de prix fixe.",
      PtBr = "Como a distância do stop é medida: Percent - porcentagem do melhor preço atingido desde a ativação (5 significa que o stop fica 5% abaixo da máxima do long), Absolute - um valor fixo em preço.",
      Uk = "Як вимірюється відстань стопа: Percent - від найкращої ціни з моменту активації (5 означає, що стоп тримається на 5% нижче максимуму лонга), Absolute - фіксована цінова величина.",
      Be = "Як вымяраецца адлегласць стопу: Percent - ад найлепшай цаны з моманту ўзвядзення (5 азначае, што стоп трымаецца на 5% ніжэй за максімум лонга), Absolute - фіксаваная цэнавая велічыня.")]
  public TrailingThresholdType ThresholdType { get; set; } = TrailingThresholdType.Percent;

  [ParameterHint(
      En = "Stop distance: percent of the best price reached since arming (Percent mode) or a fixed amount in the instrument's price currency (Absolute mode).",
      Ru = "Расстояние стопа: процент от лучшей цены с момента взведения (режим «Процент») или фиксированная величина в ценовой валюте инструмента (режим «Абсолют»).",
      Es = "Distancia del stop: porcentaje del mejor precio alcanzado desde el rearme (modo Porcentaje) o cantidad fija en la moneda del precio del instrumento (modo Absoluto).",
      It = "Distanza dello stop: percentuale del miglior prezzo raggiunto dall'attivazione (modalità Percent) o importo fisso nella valuta di prezzo dello strumento (modalità Absolute).",
      De = "Stop-Abstand: Prozentsatz des besten Kurses seit der Aktivierung (Modus Percent) oder ein fester Betrag in der Kurswährung des Instruments (Modus Absolute).",
      Fr = "Distance du stop : pourcentage du meilleur prix atteint depuis l'activation (mode Percent) ou montant fixe dans la devise de prix de l'instrument (mode Absolute).",
      PtBr = "Distância do stop: porcentagem do melhor preço atingido desde a ativação (modo Percent) ou valor fixo na moeda do preço do instrumento (modo Absolute).",
      Uk = "Відстань стопа: відсоток від найкращої ціни з моменту активації (режим Percent) або фіксована величина в валюті ціни інструменту (режим Absolute).",
      Be = "Адлегласць стопу: адсотак ад найлепшай цаны з моманту ўзвядзення (рэжым Percent) або фіксаваная велічыня ў валюце цаны інструмента (рэжым Absolute).")]
  public double Value { get; set; } = 5.0;
}
