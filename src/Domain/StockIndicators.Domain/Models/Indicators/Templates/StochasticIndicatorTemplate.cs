using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "A momentum oscillator comparing the closing price to the high-low range of the last N candles (0-100). Values above 80 indicate overbought conditions, below 20 - oversold. The alert fires when %K crosses %D leaving a zone: upward out of the oversold zone (bullish) or downward out of the overbought one (bearish); mid-range crossings and a tolerance band around the lines are filtered out.",
    Ru = "Импульсный осциллятор, сопоставляющий закрытие с диапазоном максимум-минимум последних N свечей (0-100). Значения выше 80 - перекупленность, ниже 20 - перепроданность. Алерт срабатывает, когда %K пересекает %D при выходе из зоны: вверх из перепроданности (бычий) или вниз из перекупленности (медвежий); пересечения в середине диапазона и полоса допуска отсекаются.",
    Es = "Un oscilador de momentum que compara el cierre con el rango máximo-mínimo de las últimas N velas (0-100). Valores por encima de 80 indican sobrecompra, por debajo de 20 - sobreventa. La alerta se activa cuando %K cruza %D saliendo de una zona: hacia arriba desde la sobreventa (alcista) o hacia abajo desde la sobrecompra (bajista); los cruces en mitad del rango y una banda de tolerancia se filtran.",
    It = "Oscillatore di momentum che confronta il prezzo di chiusura con l'intervallo massimo-minimo delle ultime N candele (0-100). Valori sopra 80 indicano ipercomprato, sotto 20 - ipervenduto. L'avviso scatta quando %K incrocia %D uscendo da una zona: verso l'alto dalla zona di ipervenduto (rialzista) o verso il basso da quella di ipercomprato (ribassista); gli incroci a metà intervallo e una banda di tolleranza attorno alle linee vengono filtrati.",
    De = "Ein Momentum-Oszillator, der den Schlusskurs mit der Hoch-Tief-Spanne der letzten N Kerzen vergleicht (0-100). Werte über 80 deuten auf überkaufte, unter 20 auf überverkaufte Bedingungen hin. Der Alert wird ausgelöst, wenn %K die %D kreuzt und dabei eine Zone verlässt: nach oben aus der überverkauften Zone (bullisch) oder nach unten aus der überkauften (bärisch); Kreuzungen in der Mitte des Bereichs und ein Toleranzband um die Linien werden herausgefiltert.",
    Fr = "Un oscillateur de momentum qui compare le prix de clôture à la plage plus haut/plus bas des N dernières bougies (0-100). Les valeurs au-dessus de 80 indiquent un surachat, en dessous de 20 un survente. L'alerte se déclenche quand %K croise %D en sortant d'une zone : vers le haut depuis la zone de survente (haussier) ou vers le bas depuis celle de surachat (baissier) ; les croisements en milieu de plage et une bande de tolérance autour des lignes sont filtrés.",
    PtBr = "Um oscilador de momentum que compara o preço de fechamento com a faixa máxima-mínima das últimas N velas (0-100). Valores acima de 80 indicam sobrecompra, abaixo de 20 - sobrevenda. O alerta é acionado quando o %K cruza o %D saindo de uma zona: para cima a partir da zona de sobrevenda (altista) ou para baixo a partir da de sobrecompra (baixista); cruzamentos no meio da faixa e uma banda de tolerância em torno das linhas são filtrados.",
    Uk = "Імпульсний осцилятор, що порівнює ціну закриття з діапазоном максимум-мінімум останніх N свічок (0-100). Значення вище 80 вказують на перекупленість, нижче 20 - на перепроданість. Алерт спрацьовує, коли %K перетинає %D, виходячи із зони: вгору із зони перепроданості (бичачий) або вниз із зони перекупленості (ведмежий); перетини в середині діапазону та смуга допуску відсіюються.",
    Be = "Імпульсны асцылятар, які параўноўвае цану закрыцця з дыяпазонам максімум-мінімум апошніх N свічак (0-100). Значэнні вышэй 80 паказваюць на перакупленасць, ніжэй 20 - на перепродадзенасць. Апавяшчэнне спрацоўвае, калі %K перасякае %D, выходзячы з зоны: уверх з зоны перепродадзенасці (бічыны) або ўніз з зоны перакупленасці (мядзведзі); перасячэнні ў сярэдзіне дыяпазону і паласа допуску адсяваюцца.")]
public class StochasticIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
      En = "%K calculation period in candles. Standard is 14.",
      Ru = "Период расчёта %K в свечах. Стандарт - 14.",
      Es = "Periodo de cálculo del %K en velas. El estándar es 14.",
      It = "Periodo di calcolo del %K in candele. Lo standard è 14.",
      De = "Berechnungsperiode von %K in Kerzen. Standard ist 14.",
      Fr = "Période de calcul du %K en bougies. La valeur standard est 14.",
      PtBr = "Período de cálculo do %K em velas. O padrão é 14.",
      Uk = "Період розрахунку %K у свічках. Стандарт - 14.",
      Be = "Перыяд разліку %K у свечках. Стандарт - 14.")]
  public int Period { get; set; } = 14;

  [ParameterHint(
      En = "Period of the %D line (moving average of %K). Standard is 3. The alert fires on a %K/%D crossover.",
      Ru = "Период линии %D (средняя от %K). Стандарт - 3. Алерт срабатывает при пересечении %K и %D.",
      Es = "Periodo de la línea %D (media móvil del %K). El estándar es 3. La alerta se activa con un cruce %K/%D.",
      It = "Periodo della linea %D (media mobile del %K). Lo standard è 3. L'avviso scatta su un incrocio %K/%D.",
      De = "Zeitraum der %D-Linie (gleitender Durchschnitt von %K). Standard ist 3. Der Alert wird bei einer %K/%D-Kreuzung ausgelöst.",
      Fr = "Période de la ligne %D (moyenne mobile du %K). La valeur standard est 3. L'alerte se déclenche sur un croisement %K/%D.",
      PtBr = "Período da linha %D (média móvel do %K). O padrão é 3. O alerta é acionado em um cruzamento %K/%D.",
      Uk = "Період лінії %D (ковзна середня %K). Стандарт - 3. Алерт спрацьовує при перетині %K/%D.",
      Be = "Перыяд лініі %D (слізгальная сярэдняя %K). Стандарт - 3. Апавяшчэнне спрацоўвае пры перасячэнні %K/%D.")]
  public int SignalPeriod { get; set; } = 3;

  [ParameterHint(
      En = "Period of %K smoothing. Standard is 3.",
      Ru = "Период сглаживания %K. Стандарт - 3.",
      Es = "Periodo de suavizado del %K. El estándar es 3.",
      It = "Periodo di smorzamento del %K. Lo standard è 3.",
      De = "Zeitraum der %K-Glättung. Standard ist 3.",
      Fr = "Période du lissage du %K. La valeur standard est 3.",
      PtBr = "Período de suavização do %K. O padrão é 3.",
      Uk = "Період згладжування %K. Стандарт - 3.",
      Be = "Перыяд згладжвання %K. Стандарт - 3.")]
  public int SmoothPeriod { get; set; } = 3;

  [ParameterHint(
      En = "Level above which %K is considered overbought. Standard is 80. A bearish %K/%D cross only triggers from this zone; set 100 to accept crosses from anywhere.",
      Ru = "Уровень, выше которого %K считается перекупленным. Стандарт - 80. Медвежий кросс %K/%D срабатывает только из этой зоны; поставьте 100, чтобы принимать кроссы из любой точки.",
      Es = "Nivel por encima del cual el %K se considera sobrecompra. El estándar es 80. El cruce %K/%D bajista solo se activa desde esta zona; use 100 para aceptar cruces desde cualquier punto.",
      It = "Livello al di sopra del quale il %K è considerato ipercomprato. Lo standard è 80. L'incrocio %K/%D ribassista scatta solo da questa zona; imposta 100 per accettare incroci da qualsiasi punto.",
      De = "Schwellenwert, oberhalb dessen %K als überkauft gilt. Standard ist 80. Die bärische %K/%D-Kreuzung löst nur aus dieser Zone aus; setze 100, um Kreuzungen von überall zu akzeptieren.",
      Fr = "Niveau au-dessus duquel le %K est considéré comme suracheté. La valeur standard est 80. Le croisement baissier %K/%D ne se déclenche que depuis cette zone ; mettez 100 pour accepter les croisements depuis n'importe où.",
      PtBr = "Nível acima do qual o %K é considerado sobrecomprado. O padrão é 80. O cruzamento %K/%D de baixa só é acionado a partir dessa zona; defina 100 para aceitar cruzamentos de qualquer ponto.",
      Uk = "Рівень, вище якого %K вважається перекупленим. Стандарт - 80. Ведмежий перетин %K/%D спрацьовує лише з цієї зони; встановіть 100, щоб приймати перетини з будь-якої точки.",
      Be = "Узровень, вышэй якога %K лічыцца перакупленым. Стандарт - 80. Мядзведзі перасячэнне %K/%D спрацоўвае толькі з гэтай зоны; усталяйце 100, каб прымаць перасячэнні з любога месца.")]
  public double Overbought { get; set; } = 80;

  [ParameterHint(
      En = "Level below which %K is considered oversold. Standard is 20. A bullish %K/%D cross only triggers from this zone; set 0 to accept crosses from anywhere.",
      Ru = "Уровень, ниже которого %K считается перепроданным. Стандарт - 20. Бычий кросс %K/%D срабатывает только из этой зоны; поставьте 0, чтобы принимать кроссы из любой точки.",
      Es = "Nivel por debajo del cual el %K se considera sobreventa. El estándar es 20. El cruce %K/%D alcista solo se activa desde esta zona; use 0 para aceptar cruces desde cualquier punto.",
      It = "Livello al di sotto del quale il %K è considerato ipervenduto. Lo standard è 20. L'incrocio %K/%D rialzista scatta solo da questa zona; imposta 0 per accettare incroci da qualsiasi punto.",
      De = "Schwellenwert, unterhalb dessen %K als überverkauft gilt. Standard ist 20. Die bullische %K/%D-Kreuzung löst nur aus dieser Zone aus; setze 0, um Kreuzungen von überall zu akzeptieren.",
      Fr = "Niveau en dessous duquel le %K est considéré comme survendu. La valeur standard est 20. Le croisement haussier %K/%D ne se déclenche que depuis cette zone ; mettez 0 pour accepter les croisements depuis n'importe où.",
      PtBr = "Nível abaixo do qual o %K é considerado sobrevendido. O padrão é 20. O cruzamento %K/%D de alta só é acionado a partir dessa zona; defina 0 para aceitar cruzamentos de qualquer ponto.",
      Uk = "Рівень, нижче якого %K вважається перепроданим. Стандарт - 20. Бичачий перетин %K/%D спрацьовує лише з цієї зони; встановіть 0, щоб приймати перетини з будь-якої точки.",
      Be = "Узровень, ніжэй якога %K лічыцца перепродадзеным. Стандарт - 20. Бічыны перасячэнне %K/%D спрацоўвае толькі з гэтай зоны; усталяйце 0, каб прымаць перасячэнні з любога месца.")]
  public double Oversold { get; set; } = 20;

  [ParameterHint(
      En = "Tolerance for detecting a %K/%D crossover, as a fraction of the value (0.05 = 5%). A larger value filters out noisy crossovers.",
      Ru = "Допуск при определении пересечения %K и %D, в доле от значения (0.05 = 5%). Больше значение - меньше ложных пересечений.",
      Es = "Tolerancia para detectar el cruce %K/%D, como fracción del valor (0.05 = 5%). Un valor mayor filtra cruces ruidosos.",
      It = "Tolleranza per rilevare un incrocio %K/%D, come frazione del valore (0.05 = 5%). Un valore più alto filtra gli incroci rumorosi.",
      De = "Toleranz für die Erkennung einer %K/%D-Kreuzung, als Anteil am Wert (0.05 = 5 %). Ein größerer Wert filtert verrauschte Kreuzungen heraus.",
      Fr = "Tolérance pour la détection d'un croisement %K/%D, en fraction de la valeur (0.05 = 5 %). Une valeur plus grande filtre les croisements parasites.",
      PtBr = "Tolerância para detectar um cruzamento %K/%D, como fração do valor (0.05 = 5%). Um valor maior filtra cruzamentos com ruído.",
      Uk = "Допуск для виявлення перетину %K/%D, у долі від значення (0.05 = 5%). Більше значення краще відсіює шумові перетини.",
      Be = "Допуск для выяўлення перасячэння %K/%D, у долі ад значэння (0.05 = 5%). Большае значэнне лепш адсявае шумныя перасячэнні.")]
  public double Deviation { get; set; } = 0.05;

  public int GetCandleCount() => Period + SignalPeriod + SmoothPeriod;
}
