using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "Alerts built around the 52-week extremes of the instrument, computed from its daily candles. New extreme: the close steps above the highest high (or below the lowest low) of the previous 52 weeks - a breakout traders watch closely. Pullback: the close has retreated by the given percent from the 52-week high (or rallied that far off the 52-week low). The alert fires when the condition is entered, and re-arms once the close leaves the zone.",
    Ru = "Алерты вокруг 52-недельных экстремумов инструмента, считаемые по его дневным свечам. Новый экстремум: закрытие поднимается выше максимума (или опускается ниже минимума) предыдущих 52 недель - пробой, за которым следят трейдеры. Откат: закрытие откатилось на заданный процент от 52-недельного хая (или отскочило на столько же от 52-недельного лоя). Алерт срабатывает при входе в условие и снова взводится, когда закрытие выходит из зоны.",
    Es = "Alertas en torno a los extremos de 52 semanas del instrumento, calculadas con sus velas diarias. Nuevo extremo: el cierre sube por encima del máximo (o baja por debajo del mínimo) de las 52 semanas anteriores: un breakout muy vigilado. Retroceso: el cierre se ha retirado el porcentaje indicado desde el máximo de 52 semanas (o rebotado igual desde el mínimo). La alerta se activa al entrar en la condición y se rearma cuando el cierre sale de la zona. Se necesita el historial completo: mientras la caché de velas no cubre toda la ventana, la condición permanece sin cumplir.",
    It = "Avvisi costruiti attorno agli estremi di 52 settimane dello strumento, calcolati sulle sue candele giornaliere. Nuovo estremo: la chiusura supera il massimo più alto (o scende sotto il minimo più basso) delle 52 settimane precedenti - una rottura che i trader osservano con attenzione. Ritracciamento: la chiusura si è ritracciata della percentuale indicata dal massimo di 52 settimane (o ha rimbalzato altrettanto dal minimo). L'avviso scatta quando la condizione si attiva e si riarma quando la chiusura esce dalla zona.",
    De = "Alerts rund um die 52-Wochen-Extremwerte des Instruments, berechnet auf Basis seiner Tageskerzen. Neuer Extremwert: Der Schlusskurs steigt über das höchste Hoch (oder fällt unter das tiefste Tief) der vorherigen 52 Wochen - ein Ausbruch, den Trader genau beobachten. Rücksetzer: Der Schlusskurs ist vom 52-Wochen-Hoch um den angegebenen Prozentsatz zurückgefallen (oder ebenso weit vom 52-Wochen-Tief gestiegen). Der Alert wird ausgelöst, wenn die Bedingung eintritt, und re-aktiviert sich, sobald der Schlusskurs die Zone verlässt.",
    Fr = "Alertes construites autour des extrêmes sur 52 semaines de l'instrument, calculées sur ses bougies journalières. Nouvel extrême : la clôture dépasse le plus haut (ou passe sous le plus bas) atteint au cours des 52 semaines précédentes - une rupture que les traders surveillent de près. Repli : la clôture a reculé du pourcentage indiqué depuis le plus haut sur 52 semaines (ou a rebondi d'autant depuis le plus bas sur 52 semaines). L'alerte se déclenche quand la condition est atteinte et se réarme dès que la clôture quitte la zone.",
    PtBr = "Alertas construídas em torno dos extremos de 52 semanas do instrumento, calculadas com suas velas diárias. Novo extremo: o fechamento supera a máxima mais alta (ou cai abaixo da mínima mais baixa) das 52 semanas anteriores - uma ruptura que os traders observam de perto. Recuo: o fechamento recuou a porcentagem indicada a partir da máxima de 52 semanas (ou repicou o mesmo a partir da mínima). O alerta é acionado quando a condição é atingida e rearma quando o fechamento sai da zona.",
    Uk = "Алерти навколо 52-тижневих екстремумів інструменту, що розраховуються за його денними свічками. Новий екстремум: закриття піднімається вище за найвищий максимум (або опускається нижче за найнижчий мінімум) попередніх 52 тижнів - пробій, за яким стежать трейдери. Відкат: закриття відкотилося на заданий відсоток від 52-тижневого максимуму (або адскочило на стільки ж від 52-тижневого мінімуму). Алерт спрацьовує при вході в умову та знову активується, коли закриття виходить із зони.",
    Be = "Апавяшчэнні вакол 52-тыднёвых экстрэмаў інструмента, што разлічваюцца па яго днявых свечках. Новы экстрэмум: закрыццё падымаецца вышэй за найвышэйшы максімум (або апускаецца ніжэй за найніжэйшы мінімум) папярэдніх 52 тыдняў - прабой, за якім сочаць трэйдары. Адкат: закрыццё адышло на зададзены адсотак ад 52-тыднёвага максімуму (або адскочыла на столькі ж ад 52-тыднёвага мінімуму). Апавяшчэнне спрацоўвае пры ўваходзе ў умову і зноў узводзіцца, калі закрыццё выходзіць з зоны.")]
public class Week52HighLowIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
{
  [ParameterHint(
      En = "Candle timeframe the indicator is calculated on. Daily candles give the classic 52-week window on a trading-day history.",
      Ru = "Таймфрейм свечей, по которым считается индикатор. Дневные свечи дают классическое 52-недельное окно по истории торговых дней.",
      Es = "Temporalidad de las velas con la que se calcula el indicador. Las velas diarias dan la ventana clásica de 52 semanas sobre el historial de días de negociación.",
      It = "Timeframe delle candele su cui viene calcolato l'indicatore. Le candele giornaliere danno la classica finestra di 52 settimane sullo storico dei giorni di borsa.",
      De = "Zeitrahmen der Kerzen, auf dem der Indikator berechnet wird. Tageskerzen ergeben das klassische 52-Wochen-Fenster auf der Historie der Handelstage.",
      Fr = "Unité de temps des bougies sur laquelle l'indicateur est calculé. Les bougies journalières donnent la fenêtre classique de 52 semaines sur l'historique des jours de cotation.",
      PtBr = "Timeframe das velas em que o indicador é calculado. As velas diárias dão a janela clássica de 52 semanas sobre o histórico de dias de pregão.",
      Uk = "Таймфрейм свічок, на якім розраховується індикатор. Денні свічки дають класичне 52-тижневе вікно на історії торгових днів.",
      Be = "Таймфрэйм свечак, на якім разлічваецца індыкатар. Днявыя свечкі даюць класічнае 52-тыднёвае акно на гісторыі гандлёвых дзён.")]
  public TimeFrame TimeFrame { get; set; } = TimeFrame.Day;

  [ParameterHint(
      En = "Lookback in weeks (52 = one year). The window spans this many calendar weeks before the current candle; the current candle itself is always excluded from the extremes.",
      Ru = "Глубина в неделях (52 = год). Окно охватывает столько календарных недель до текущей свечи; сама текущая свеча из экстремумов всегда исключается.",
      Es = "Retroceso en semanas (52 = un año). La ventana abarca tantas semanas naturales antes de la vela actual; la vela actual se excluye siempre de los extremos.",
      It = "Profondità di ricerca a ritroso in settimane (52 = un anno). La finestra abbraccia questo numero di settimane di calendario prima della candela corrente; la candela corrente stessa è sempre esclusa dagli estremi.",
      De = "Rückblick in Wochen (52 = ein Jahr). Das Fenster umfasst so viele Kalenderwochen vor der aktuellen Kerze; die aktuelle Kerze selbst wird aus den Extremwerten stets ausgeschlossen.",
      Fr = "Profondeur de l'analyse en semaines (52 = un an). La fenêtre couvre ce nombre de semaines calendaires avant la bougie actuelle ; la bougie actuelle elle-même est toujours exclue des extrêmes.",
      PtBr = "Recuo em semanas (52 = um ano). A janela abrange essa quantidade de semanas de calendário antes da vela atual; a própria vela atual fica sempre excluída dos extremos.",
      Uk = "Глибина пошуку в тижнях (52 = рік). Вікно охоплює стільки календарних тижнів до поточної свічки; сама поточна свічка завжди виключається з екстремумів.",
      Be = "Глыбіня пошуку ў тыднях (52 = год). Акно ахоплівае столькі каляндарных тыдняў да бягучай свечкі; сама бягучая свечка заўсёды выключваецца з экстрэмаў.")]
  public int Weeks { get; set; } = 52;

  [ParameterHint(
      En = "Which extreme to watch: Both - highs and lows, High - only the 52-week high, Low - only the 52-week low.",
      Ru = "Какой экстремум отслеживать: «Оба» - и хаи, и лои, «Хай» - только 52-недельный хай, «Лой» - только 52-недельный лой.",
      Es = "Qué extremo vigilar: «Ambos» - máximos y mínimos, «Máximo» - solo el máximo de 52 semanas, «Mínimo» - solo el mínimo de 52 semanas.",
      It = "Quale estremo monitorare: Both - massimi e minimi, High - solo il massimo di 52 settimane, Low - solo il minimo di 52 settimane.",
      De = "Zu beobachtender Extremwert: Both - Hochs und Tiefs, High - nur das 52-Wochen-Hoch, Low - nur das 52-Wochen-Tief.",
      Fr = "Extrême à surveiller : Both - plus hauts et plus bas, High - uniquement le plus haut sur 52 semaines, Low - uniquement le plus bas sur 52 semaines.",
      PtBr = "Qual extremo monitorar: Both - máximas e mínimas, High - apenas a máxima de 52 semanas, Low - apenas a mínima de 52 semanas.",
      Uk = "Який екстремум відстежувати: Both - і максимуми, і мінімуми, High - лише 52-тижневий максимум, Low - лише 52-тижневий мінімум.",
      Be = "За якім экстрэмумам назіраць: Both - і максіумы, і мініумы, High - толькі 52-тыднёвы максімум, Low - толькі 52-тыднёвы мінімум.")]
  public Week52Extreme Extreme { get; set; } = Week52Extreme.Both;

  [ParameterHint(
      En = "Trigger mode: New extreme - the close sets a fresh 52-week high/low; Pullback - the close has pulled back from the extreme by at least the percent below.",
      Ru = "Режим срабатывания: «Новый экстремум» - закрытие ставит новый 52-недельный хай/лой; «Откат» - закрытие откатилось от экстремума минимум на процент ниже.",
      Es = "Modo de disparo: Nuevo extremo - el cierre marca un nuevo máximo/mínimo de 52 semanas; Retroceso - el cierre se ha retirado del extremo al menos el porcentaje de abajo.",
      It = "Modalità di attivazione: nuovo estremo - la chiusura segna un nuovo massimo/minimo di 52 settimane; ritracciamento - la chiusura si è ritracciata dall'estremo di almeno la percentuale indicata qui sotto.",
      De = "Auslösungsmodus: Neuer Extremwert - der Schlusskurs markiert ein neues 52-Wochen-Hoch/-Tief; Rücksetzer - der Schlusskurs ist vom Extremwert um mindestens den unten angegebenen Prozentsatz zurückgefallen.",
      Fr = "Mode de déclenchement : nouvel extrême - la clôture marque un nouveau plus haut/plus bas sur 52 semaines ; repli - la clôture a reculé de l'extrême d'au moins le pourcentage indiqué ci-dessous.",
      PtBr = "Modo de disparo: novo extremo - o fechamento marca uma nova máxima/mínima de 52 semanas; recuo - o fechamento recuou do extremo pelo menos a porcentagem indicada abaixo.",
      Uk = "Режим спрацювання: новий екстремум - закриття встановлює новий 52-тижневий максимум/мінімум; відкат - закриття відкотілося від екстремуму щонайменше на відсоток, зазначений нижче.",
      Be = "Рэжым спрацоўвання: новы экстрэмум - закрыццё ўсталёўвае новы 52-тыднёвы максімум/мінімум; адкат - закрыццё адышло ад экстрэмуму як мінімум на працэнт, пазначаны ніжэй.")]
  public Week52Mode Mode { get; set; } = Week52Mode.NewExtreme;

  [ParameterHint(
      En = "Pullback threshold in percent from the extreme (only used in the Pullback mode). The alert fires when the retreat from the 52-week high - or the bounce from the 52-week low - crosses this magnitude.",
      Ru = "Порог отката в процентах от экстремума (используется только в режиме «Откат»). Алерт срабатывает, когда откат от 52-недельного хая (или отскок от 52-недельного лоя) пересекает эту величину.",
      Es = "Umbral de retroceso en porcentaje desde el extremo (solo se usa en el modo Retroceso). La alerta se activa cuando el retroceso desde el máximo de 52 semanas - o el rebote desde el mínimo - cruza esta magnitud.",
      It = "Soglia di ritracciamento in percentuale dall'estremo (usata solo nella modalità ritracciamento). L'avviso scatta quando l'arretramento dal massimo di 52 settimane - o il rimbalzo dal minimo di 52 settimane - supera questa misura.",
      De = "Schwellenwert für den Rücksetzer in Prozent vom Extremwert (nur im Modus Rücksetzer verwendet). Der Alert wird ausgelöst, wenn der Rückgang vom 52-Wochen-Hoch - oder der Anstieg vom 52-Wochen-Tief - dieses Ausmaß überschreitet.",
      Fr = "Seuil de repli en pourcentage depuis l'extrême (utilisé uniquement dans le mode repli). L'alerte se déclenche quand le recul depuis le plus haut sur 52 semaines - ou le rebond depuis le plus bas sur 52 semaines - atteint cette ampleur.",
      PtBr = "Limiar de recuo em porcentagem a partir do extremo (usado apenas no modo recuo). O alerta é acionado quando o recuo a partir da máxima de 52 semanas - ou o repique a partir da mínima de 52 semanas - ultrapassa essa magnitude.",
      Uk = "Поріг відкату у відсотках ад екстремуму (використовується лише в режимі відкату). Алерт спрацьовує, коли відкат від 52-тижневого максимуму (або відскок від 52-тижневого мінімуму) перевищує цю величину.",
      Be = "Парог адкату ў адсотках ад экстрэмуму (выкарыстоўваецца толькі ў рэжыме адкату). Апавяшчэнне спрацоўвае, калі адкат ад 52-тыднёвага максімуму (або адскок ад 52-тыднёвага мінімуму) перавышае гэтую велічыню.")]
  public double PullbackPercent { get; set; } = 5.0;

  // One fetch must cover the whole calendar window (24/7 venues have a candle on every day)
  // plus about two months of margin for provider holes; the handler itself only trusts an
  // extreme once the window reaches into the span's last 10%. The Day slot window is deeper.
  public int GetCandleCount() => Weeks * 7 + 56;
}
