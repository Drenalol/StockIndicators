using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "A stochastic applied to RSI: a very sensitive momentum oscillator in the 0-100 range. Values near 0 indicate oversold conditions, near 100 - overbought. The alert fires when %K crosses %D leaving a channel: upward out of the lower channel (bullish) or downward out of the upper one (bearish); mid-range crossings and a tolerance band around the lines filter out noisy signals.",
    Ru = "Стохастик, применённый к RSI: очень чувствительный импульсный осциллятор в диапазоне 0-100. Значения близки к 0 - перепроданность, к 100 - перекупленность. Алерт срабатывает, когда %K пересекает %D при выходе из канала: вверх из нижней зоны (бычий) или вниз из верхней (медвежий); пересечения в середине диапазона и полоса допуска отсекают шумные сигналы.",
    Es = "Un estocástico aplicado al RSI: un oscilador de momentum muy sensible en el rango 0-100. Valores cercanos a 0 indican sobreventa, cercanos a 100 - sobrecompra. La alerta se activa cuando %K cruza %D saliendo de un canal: hacia arriba desde la zona inferior (alcista) o hacia abajo desde la superior (bajista); los cruces en mitad del rango y una banda de tolerancia filtran las señales ruidosas.",
    It = "Uno stocastico applicato all'RSI: un oscillatore di momentum molto sensibile nell'intervallo 0-100. Valori vicini a 0 indicano ipervenduto, vicini a 100 - ipercomprato. L'avviso scatta quando %K incrocia %D uscendo da un canale: verso l'alto dal canale inferiore (rialzista) o verso il basso da quello superiore (ribassista); gli incroci a metà intervallo e una banda di tolleranza attorno alle linee filtrano i segnali rumorosi.",
    De = "Eine Stochastik auf den RSI: ein sehr empfindlicher Momentum-Oszillator im Bereich 0-100. Werte nahe 0 deuten auf überverkaufte, nahe 100 auf überkaufte Bedingungen hin. Der Alert wird ausgelöst, wenn %K die %D kreuzt und dabei einen Kanal verlässt: nach oben aus dem unteren Kanal (bullisch) oder nach unten aus dem oberen (bärisch); Kreuzungen in der Mitte des Bereichs und ein Toleranzband um die Linien filtern verrauschte Signale heraus.",
    Fr = "Un stochastique appliqué au RSI : un oscillateur de momentum très sensible dans la plage 0-100. Les valeurs proches de 0 indiquent une survente, proches de 100 un surachat. L'alerte se déclenche quand %K croise %D en sortant d'un canal : vers le haut depuis le canal inférieur (haussier) ou vers le bas depuis le canal supérieur (baissier) ; les croisements en milieu de plage et une bande de tolérance autour des lignes filtrent les signaux parasites.",
    PtBr = "Um estocástico aplicado ao RSI: um oscilador de momentum muito sensível na faixa 0-100. Valores perto de 0 indicam sobrevenda, perto de 100 - sobrecompra. O alerta é acionado quando o %K cruza o %D saindo de um canal: para cima a partir do canal inferior (altista) ou para baixo a partir do superior (baixista); cruzamentos no meio da faixa e uma banda de tolerância em torno das linhas filtram sinais com ruído.",
    Uk = "Стохастик, застосований до RSI: дуже чутливий імпульсний осцилятор у діапазоні 0-100. Значення біля 0 вказують на перепроданість, біля 100 - на перекупленість. Алерт спрацьовує, коли %K перетинає %D, виходячи з каналу: вгору з нижнього каналу (бичачий) або вниз з верхнього (ведмежий); перетини в середині діапазону та смуга допуску відсіюють шумові сигнали.",
    Be = "Стохастык, прыменены да RSI: вельмі адчувальны імпульсны асцылятар у дыяпазоне 0-100. Значэнні каля 0 паказваюць на перепродадзенасць, каля 100 - на перакупленасць. Апавяшчэнне спрацоўвае, калі %K перасякае %D, выходзячы з канала: уверх з ніжняга канала (бічыны) або ўніз з верхняга (мядзведзі); перасячэнні ў сярэдзіне дыяпазону і паласа допуску адсяваюць шумныя сігналы.")]
public class StochRsiIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
{
  [ParameterHint(
      En = "Candle timeframe the indicator is calculated on. The alert is evaluated on each closed candle of this period.",
      Ru = "Таймфрейм свечей, по которым считается индикатор. Проверка выполняется при закрытии каждой свечи этого периода.",
      Es = "Temporalidad de las velas con la que se calcula el indicador. La alerta se evalúa al cierre de cada vela de este periodo.",
      It = "Timeframe della candela su cui viene calcolato l'indicatore. L'avviso viene valutato alla chiusura di ogni candela di questo periodo.",
      De = "Zeitrahmen der Kerzen, auf dem der Indikator berechnet wird. Der Alert wird beim Schluss jeder Kerze dieses Zeitraums ausgewertet.",
      Fr = "Unité de temps des bougies sur laquelle l'indicateur est calculé. L'alerte est évaluée à la clôture de chaque bougie de cette période.",
      PtBr = "Timeframe das velas no qual o indicador é calculado. O alerta é avaliado no fechamento de cada vela desse período.",
      Uk = "Таймфрейм свічок, на якому розраховується індикатор. Алерт перевіряється на кожній закритій свічці цього періоду.",
      Be = "Таймфрэйм свічак, на якім разлічваецца індыкатар. Апавяшчэнне правяраецца на кожнай зачыненай свічцы гэтага перыяду.")]
  public TimeFrame TimeFrame { get; set; }

  [ParameterHint(
      En = "Period of the inner RSI component.",
      Ru = "Период внутреннего компонента RSI.",
      Es = "Periodo del componente RSI interno.",
      It = "Periodo della componente RSI interna.",
      De = "Zeitraum der internen RSI-Komponente.",
      Fr = "Période du composant RSI interne.",
      PtBr = "Período do componente RSI interno.",
      Uk = "Період внутрішнього компонента RSI.",
      Be = "Перыяд унутранага кампанента RSI.")]
  public int RsiPeriod { get; set; }

  [ParameterHint(
      En = "Period of the stochastic component applied to RSI.",
      Ru = "Период стохастического компонента, применяемого к RSI.",
      Es = "Periodo del componente estocástico aplicado al RSI.",
      It = "Periodo della componente stocastica applicata all'RSI.",
      De = "Zeitraum der Stochastik-Komponente, die auf den RSI angewendet wird.",
      Fr = "Période du composant stochastique appliqué au RSI.",
      PtBr = "Período do componente estocástico aplicado ao RSI.",
      Uk = "Період стохастичного компонента, що застосовується до RSI.",
      Be = "Перыяд стохастычнага кампанента, які прымяняецца да RSI.")]
  public int StochPeriod { get; set; }

  [ParameterHint(
      En = "Period of the %D signal line (moving average of StochRSI). The alert fires on a %K/%D crossover.",
      Ru = "Период сигнальной линии %D (средняя от StochRSI). Алерт срабатывает при пересечении %K и %D.",
      Es = "Periodo de la línea de señal %D (media móvil del StochRSI). La alerta se activa con un cruce %K/%D.",
      It = "Periodo della linea del segnale %D (media mobile dello StochRSI). L'avviso scatta su un incrocio %K/%D.",
      De = "Zeitraum der Signallinie %D (gleitender Durchschnitt von StochRSI). Der Alert wird bei einer %K/%D-Kreuzung ausgelöst.",
      Fr = "Période de la ligne de signal %D (moyenne mobile du StochRSI). L'alerte se déclenche sur un croisement %K/%D.",
      PtBr = "Período da linha de sinal %D (média móvel do StochRSI). O alerta é acionado em um cruzamento %K/%D.",
      Uk = "Період сигнальної лінії %D (ковзна середня StochRSI). Алерт спрацьовує при перетині %K/%D.",
      Be = "Перыяд сігнальнай лініі %D (слізгальная сярэдняя StochRSI). Апавяшчэнне спрацоўвае пры перасячэнні %K/%D.")]
  public int SignalPeriod { get; set; }

  [ParameterHint(
      En = "Period of smoothing (moving average of the %D line).",
      Ru = "Период сглаживания (средняя от линии %D).",
      Es = "Periodo de suavizado (media móvil de la línea %D).",
      It = "Periodo di smorzamento (media mobile della linea %D).",
      De = "Zeitraum der Glättung (gleitender Durchschnitt der %D-Linie).",
      Fr = "Période de lissage (moyenne mobile de la ligne %D).",
      PtBr = "Período de suavização (média móvel da linha %D).",
      Uk = "Період згладжування (ковзна середня лінії %D).",
      Be = "Перыяд згладжвання (слізгальная сярэдняя лініі %D).")]
  public int SmoothPeriod { get; set; }

  [ParameterHint(
      En = "Tolerance for detecting a %K/%D crossover, as a fraction of the value (0.1 = 10%). A larger value filters out noisy crossovers.",
      Ru = "Допуск при определении пересечения %K и %D, в доле от значения (0.1 = 10%). Больше значение - меньше ложных пересечений.",
      Es = "Tolerancia para detectar el cruce %K/%D, como fracción del valor (0.1 = 10%). Un valor mayor filtra cruces ruidosos.",
      It = "Tolleranza per rilevare un incrocio %K/%D, come frazione del valore (0.1 = 10%). Un valore più alto filtra gli incroci rumorosi.",
      De = "Toleranz für die Erkennung einer %K/%D-Kreuzung, als Anteil am Wert (0.1 = 10 %). Ein größerer Wert filtert verrauschte Kreuzungen heraus.",
      Fr = "Tolérance pour la détection d'un croisement %K/%D, en fraction de la valeur (0.1 = 10 %). Une valeur plus grande filtre les croisements parasites.",
      PtBr = "Tolerância para detectar um cruzamento %K/%D, como fração do valor (0.1 = 10%). Um valor maior filtra cruzamentos com ruído.",
      Uk = "Допуск для виявлення перетину %K/%D, у долі від значення (0.1 = 10%). Більше значення краще відсіює шумові перетини.",
      Be = "Допуск для выяўлення перасячэння %K/%D, у долі ад значэння (0.1 = 10%). Большае значэнне лепш адсявае шумныя перасячэнні.")]
  public double Deviation { get; set; } = 0.1F;

  [ParameterHint(
      En = "Oversold channel boundary: a bullish %K/%D cross only triggers when %K rises out of the zone below this level. Set 0 to accept crosses from anywhere.",
      Ru = "Нижняя граница канала (перепроданность): бычий кросс %K/%D срабатывает, только когда %K выходит вверх из зоны ниже этого уровня. Поставьте 0, чтобы принимать кроссы из любой точки.",
      Es = "Frontera inferior del canal (sobreventa): el cruce %K/%D alcista solo se activa cuando %K sale hacia arriba de la zona por debajo de este nivel. Use 0 para aceptar cruces desde cualquier punto.",
      It = "Confine inferiore del canale (ipervenduto): l'incrocio %K/%D rialzista scatta solo quando %K risale uscendo dalla zona sotto questo livello. Imposta 0 per accettare incroci da qualsiasi punto.",
      De = "Untere Kanalgrenze (überverkauft): Die bullische %K/%D-Kreuzung löst nur aus, wenn %K aus der Zone unterhalb dieses Werts nach oben heraussteigt. Setze 0, um Kreuzungen von überall zu akzeptieren.",
      Fr = "Limite inférieure du canal (survente) : le croisement haussier %K/%D ne se déclenche que lorsque %K remonte hors de la zone située sous ce niveau. Mettez 0 pour accepter les croisements depuis n'importe où.",
      PtBr = "Fronteira inferior do canal (sobrevenda): o cruzamento %K/%D de alta só é acionado quando o %K sobe saindo da zona abaixo desse nível. Defina 0 para aceitar cruzamentos de qualquer ponto.",
      Uk = "Нижня межа каналу (перепроданість): бичачий перетин %K/%D спрацьовує, лише коли %K виходить вгору із зони нижче цього рівня. Встановіть 0, щоб приймати перетини з будь-якої точки.",
      Be = "Ніжняя мяжа канала (перепродадзенасць): бічыны перасячэнне %K/%D спрацоўвае, толькі калі %K выходзіць уверх з зоны ніжэй гэтага ўзроўню. Усталюйце 0, каб прымаць перасячэнні з любога месца.")]
  public decimal MinChannel { get; set; } = 20;

  [ParameterHint(
      En = "Overbought channel boundary: a bearish %K/%D cross only triggers when %K falls out of the zone above this level. Set 100 to accept crosses from anywhere.",
      Ru = "Верхняя граница канала (перекупленность): медвежий кросс %K/%D срабатывает, только когда %K выходит вниз из зоны выше этого уровня. Поставьте 100, чтобы принимать кроссы из любой точки.",
      Es = "Frontera superior del canal (sobrecompra): el cruce %K/%D bajista solo se activa cuando %K sale hacia abajo de la zona por encima de este nivel. Use 100 para aceptar cruces desde cualquier punto.",
      It = "Confine superiore del canale (ipercomprato): l'incrocio %K/%D ribassista scatta solo quando %K scende uscendo dalla zona sopra questo livello. Imposta 100 per accettare incroci da qualsiasi punto.",
      De = "Obere Kanalgrenze (überkauft): Die bärische %K/%D-Kreuzung löst nur aus, wenn %K aus der Zone oberhalb dieses Werts nach unten herausfällt. Setze 100, um Kreuzungen von überall zu akzeptieren.",
      Fr = "Limite supérieure du canal (surachat) : le croisement baissier %K/%D ne se déclenche que lorsque %K baisse hors de la zone située au-dessus de ce niveau. Mettez 100 pour accepter les croisements depuis n'importe où.",
      PtBr = "Fronteira superior do canal (sobrecompra): o cruzamento %K/%D de baixa só é acionado quando o %K cai saindo da zona acima desse nível. Defina 100 para aceitar cruzamentos de qualquer ponto.",
      Uk = "Верхня межа каналу (перекупленість): ведмежий перетин %K/%D спрацьовує, лише коли %K виходить вниз із зони вище цього рівня. Встановіть 100, щоб приймати перетини з будь-якої точки.",
      Be = "Верхняя мяжа канала (перакупленасць): мядзведзі перасячэнне %K/%D спрацоўвае, толькі калі %K выходзіць уніз з зоны вышэй гэтага ўзроўню. Усталюйце 100, каб прымаць перасячэнні з любога месца.")]
  public decimal MaxChannel { get; set; } = 80;

  public int GetCandleCount() => (RsiPeriod + StochPeriod) * 2;
}