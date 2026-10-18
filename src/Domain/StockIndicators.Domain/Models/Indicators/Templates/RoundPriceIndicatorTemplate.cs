using StockIndicators.Domain.Models.Indicators.Attributes;

namespace StockIndicators.Domain.Models.Indicators.Templates;

[IndicatorHint(
    En = "Detects when a candle closes at an exact multiple of the accuracy value (e.g. 100 → 1000, 1100, 1200…). Round prices are psychological levels where orders and market reactions often cluster. The alert fires on every candle that closes exactly on a round price.",
    Ru = "Отмечает, когда свеча закрывается точно на кратном значении точности (например, 100 → 1000, 1100, 1200…). Круглые цены - психологические уровни, вокруг которых часто скапливаются ордеры и реакции рынка. Алерт срабатывает на каждой свече, закрывшейся ровно на круглой цене.",
    Es = "Detecta cuando una vela cierra exactamente en un múltiplo del valor de precisión (p. ej. 100 → 1000, 1100, 1200…). Los precios redondos son niveles psicológicos donde suelen acumularse órdenes y reacciones del mercado. La alerta se activa en cada vela que cierra exactamente en un precio redondo.",
    It = "Rileva quando una candela chiude esattamente su un multiplo del valore di precisione (es. 100 → 1000, 1100, 1200…). I prezzi tondi sono livelli psicologici intorno ai quali spesso si concentrano ordini e reazioni del mercato. L'avviso scatta su ogni candela che chiude esattamente su un prezzo tondo.",
    De = "Erkennt, wenn eine Kerze genau auf einem Vielfachen des Genauigkeitswerts schließt (z. B. 100 → 1000, 1100, 1200…). Runde Kurse sind psychologische Marken, um die sich oft Orders und Marktreaktionen sammeln. Der Alert wird bei jeder Kerze ausgelöst, die genau auf einem runden Kurs schließt.",
    Fr = "Détecte quand une bougie clôture exactement sur un multiple de la valeur de précision (par ex. 100 → 1000, 1100, 1200…). Les prix ronds sont des niveaux psychologiques où se concentrent souvent les ordres et les réactions du marché. L'alerte se déclenche sur chaque bougie qui clôture exactement sur un prix rond.",
    PtBr = "Detecta quando uma vela fecha exatamente em um múltiplo do valor de precisão (ex.: 100 → 1000, 1100, 1200…). Preços redondos são níveis psicológicos onde frequentemente se acumulam ordens e reações do mercado. O alerta é acionado em cada vela que fecha exatamente em um preço redondo.",
    Uk = "Виявляє, коли свічка закривається рівно на кратному значення точності (напр. 100 → 1000, 1100, 1200…). Круглі ціни - психологічні рівні, навколо яких часто скупчуються ордери та реакції ринку. Алерт спрацьовує на кожній свічці, що закрилася рівно на круглій ціні.",
    Be = "Выяўляе, калі свечка закрываецца роўна на кратным значэння дакладнасці (напр. 100 → 1000, 1100, 1200…). Круглыя цэны - псіхалагічныя ўзроўні, вакол якіх часта скопліваюцца ордэры і рэакцыі рынку. Апавяшчэнне спрацоўвае на кожнай свечцы, якая закрылася роўна на круглай цане.")]
public class RoundPriceIndicatorTemplate : IIndicatorTemplate
{
  [ParameterHint(
      En = "A price is considered round if it is exactly divisible by this value (e.g. 100 → alerts at 1000, 1100, 1200…).",
      Ru = "Цена считается круглой, если делится на это значение нацело (например, 100 → алерты на 1000, 1100, 1200…).",
      Es = "Un precio se considera redondo si es divisible exactamente por este valor (p. ej. 100 → alertas en 1000, 1100, 1200…).",
      It = "Un prezzo è considerato tondo se è divisibile esattamente per questo valore (es. 100 → avvisi a 1000, 1100, 1200…).",
      De = "Ein Kurs gilt als rund, wenn er ohne Rest durch diesen Wert teilbar ist (z. B. 100 → Alerts bei 1000, 1100, 1200…).",
      Fr = "Un prix est considéré comme rond s'il est divisible exactement par cette valeur (par ex. 100 → alertes à 1000, 1100, 1200…).",
      PtBr = "Um preço é considerado redondo se é divisível exatamente por esse valor (ex.: 100 → alertas em 1000, 1100, 1200…).",
      Uk = "Ціна вважається круглою, якщо ділиться на це значення без остачі (напр. 100 → алерти на 1000, 1100, 1200…).",
      Be = "Цана лічыцца круглай, калі дзеліцца на гэтае значэнне без астачы (напр. 100 → апавяшчэнні на 1000, 1100, 1200…).")]
  public decimal Accuracy { get; set; }
}