using StockIndicators.Domain.Models.Candles;
using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "The difference between the fast (12) and slow (26) exponential moving averages, plus its signal line - the 9-period moving average of the MACD line. The histogram shows the momentum between the two lines. The alert fires when the MACD line crosses the signal line or the histogram changes sign.",
    Ru = "Разница между быстрой (12) и медленной (26) экспоненциальными средними, плюс линия сигнала - 9-периодная средняя от линии MACD. Гистограмма показывает импульс между линиями. Алерт срабатывает при пересечении линии MACD с сигнальной линией или при смене знака гистограммы.",
    Es = "La diferencia entre las medias móviles exponenciales rápida (12) y lenta (26), más su línea de señal - la media móvil de 9 periodos de la línea MACD. El histograma muestra el impulso entre ambas líneas. La alerta se activa cuando la línea MACD cruza la línea de señal o el histograma cambia de signo.",
    It = "La differenza tra la media mobile esponenziale veloce (12) e quella lenta (26), più la sua linea del segnale - la media mobile a 9 periodi della linea MACD. L'istogramma mostra il momentum tra le due linee. L'avviso scatta quando la linea MACD incrocia la linea del segnale o l'istogramma cambia segno.",
    De = "Die Differenz zwischen dem schnellen (12) und dem langsamen (26) exponentiellen gleitenden Durchschnitt, plus seine Signallinie - der gleitende Durchschnitt über 9 Perioden der MACD-Linie. Das Histogramm zeigt das Momentum zwischen den beiden Linien. Der Alert wird ausgelöst, wenn die MACD-Linie die Signallinie kreuzt oder das Histogramm das Vorzeichen wechselt.",
    Fr = "La différence entre la moyenne mobile exponentielle rapide (12) et la lente (26), plus sa ligne de signal - la moyenne mobile à 9 périodes de la ligne MACD. L'histogramme montre le momentum entre les deux lignes. L'alerte se déclenche lorsque la ligne MACD croise la ligne de signal ou que l'histogramme change de signe.",
    PtBr = "A diferença entre a média móvel exponencial rápida (12) e a lenta (26), mais a sua linha de sinal - a média móvel de 9 períodos da linha MACD. O histograma mostra o momentum entre as duas linhas. O alerta é acionado quando a linha MACD cruza a linha de sinal ou o histograma muda de sinal.",
    Uk = "Різниця між швидкою (12) та повільною (26) експоненційними ковзними середніми, плюс сигнальна лінія - 9-періодна ковзна середня лінії MACD. Гістограма показує імпульс між двома лініями. Алерт спрацьовує, коли лінія MACD перетинає сигнальну лінію або гістограма змінює знак.",
    Be = "Розніца паміж хуткай (12) і павольнай (26) экспанэнцыяльнымі слізгальнымі сярэднімі, плюс сігнальная лінія - 9-перыядная слізгальная сярэдняя лініі MACD. Гістаграма паказвае імпульс паміж дзвюма лініямі. Апавяшчэнне спрацоўвае, калі лінія MACD перасякае сігнальную лінію або гістаграма мяняе знак.")]
public class MacdIndicatorTemplate : ICandleCountDependentIndicatorTemplate, ITimeFrameDependentIndicatorTemplate
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
      En = "Period of the fast EMA. Standard is 12.",
      Ru = "Период быстрой EMA. Стандарт - 12.",
      Es = "Periodo de la EMA rápida. El estándar es 12.",
      It = "Periodo della EMA veloce. Lo standard è 12.",
      De = "Periode der schnellen EMA. Standard ist 12.",
      Fr = "Période de l'EMA rapide. Le standard est 12.",
      PtBr = "Período da EMA rápida. O padrão é 12.",
      Uk = "Період швидкої EMA. Стандарт - 12.",
      Be = "Перыяд хуткай EMA. Стандарт - 12.")]
  public int FastPeriods { get; set; } = 12;

  [ParameterHint(
      En = "Period of the slow EMA. Standard is 26.",
      Ru = "Период медленной EMA. Стандарт - 26.",
      Es = "Periodo de la EMA lenta. El estándar es 26.",
      It = "Periodo della EMA lenta. Lo standard è 26.",
      De = "Periode der langsamen EMA. Standard ist 26.",
      Fr = "Période de l'EMA lente. Le standard est 26.",
      PtBr = "Período da EMA lenta. O padrão é 26.",
      Uk = "Період повільної EMA. Стандарт - 26.",
      Be = "Перыяд павольнай EMA. Стандарт - 26.")]
  public int SlowPeriods { get; set; } = 26;

  [ParameterHint(
      En = "Period of the signal line (moving average of the MACD line). Standard is 9. The alert fires when the MACD line crosses the signal line.",
      Ru = "Период сигнальной линии (средняя от линии MACD). Стандарт - 9. Алерт срабатывает при пересечении линии MACD сигнальной линии.",
      Es = "Periodo de la línea de señal (media móvil de la línea MACD). El estándar es 9. La alerta se activa cuando la línea MACD cruza la línea de señal.",
      It = "Periodo della linea del segnale (media mobile della linea MACD). Lo standard è 9. L'avviso scatta quando la linea MACD incrocia la linea del segnale.",
      De = "Periode der Signallinie (gleitender Durchschnitt der MACD-Linie). Standard ist 9. Der Alert wird ausgelöst, wenn die MACD-Linie die Signallinie kreuzt.",
      Fr = "Période de la ligne de signal (moyenne mobile de la ligne MACD). Le standard est 9. L'alerte se déclenche lorsque la ligne MACD croise la ligne de signal.",
      PtBr = "Período da linha de sinal (média móvel da linha MACD). O padrão é 9. O alerta é acionado quando a linha MACD cruza a linha de sinal.",
      Uk = "Період сигнальної лінії (ковзна середня лінії MACD). Стандарт - 9. Алерт спрацьовує, коли лінія MACD перетинає сигнальну лінію.",
      Be = "Перыяд сігнальнай лініі (слізгальная сярэдняя лініі MACD). Стандарт - 9. Апавяшчэнне спрацоўвае, калі лінія MACD перасякае сігнальную лінію.")]
  public int SignalPeriods { get; set; } = 9;

  [ParameterHint(
      En = "Which crossover fires the alert: Up - bullish crossovers only (MACD above signal or histogram turning positive), Down - bearish crossovers only, Both - both directions.",
      Ru = "Какой кросс даёт алерт: «Рост» - только бычьи кроссы (MACD пересекает сигнал вверх или гистограмма уходит в плюс), «Падение» - только медвежьи кроссы, «Оба» - оба направления.",
      Es = "Qué cruce activa la alerta: «Subida» - solo cruces alcistas (MACD cruza la señal al alza o el histograma pasa a positivo), «Caída» - solo cruces bajistas, «Ambos» - las dos direcciones.",
      It = "Quale incrocio attiva l'avviso: Up - solo incroci rialzisti (MACD sopra il segnale o istogramma che diventa positivo), Down - solo incroci ribassisti, Both - entrambe le direzioni.",
      De = "Welche Kreuzung den Alert auslöst: Up - nur bullische Kreuzungen (MACD über dem Signal oder Histogramm dreht ins Plus), Down - nur bärische Kreuzungen, Both - beide Richtungen.",
      Fr = "Quel croisement déclenche l'alerte : Up - croisements haussiers uniquement (MACD au-dessus du signal ou histogramme passant en positif), Down - croisements baissiers uniquement, Both - les deux directions.",
      PtBr = "Qual cruzamento aciona o alerta: Up - só cruzamentos altistas (MACD acima do sinal ou histograma virando positivo), Down - só cruzamentos baixistas, Both - as duas direções.",
      Uk = "Який перетин дає алерт: Up - лише бичачі перетини (MACD вище за сигнал або гістограма переходить у плюс), Down - лише ведмежі перетини, Both - обидва напрямки.",
      Be = "Якое перасячэнне дае апавяшчэнне: Up - толькі бічыныя перасячэнні (MACD вышэй за сігнал або гістаграма пераходзіць у плюс), Down - толькі мядзведзія перасячэнні, Both - абодва кірункі.")]
  public AlertDirection Direction { get; set; } = AlertDirection.Both;

  public int GetCandleCount() => SlowPeriods + SignalPeriods + 10;
}
