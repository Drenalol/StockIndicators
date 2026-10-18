using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockIndicators.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedRulePresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "RulePresets" ("Id", "Key", "Indicator", "Order", "NameEn", "NameRu", "NameEs", "DescriptionEn", "DescriptionRu", "DescriptionEs", "RuleData", "Created") VALUES
                ('dd4480a7-6e7f-7dc8-b56b-aafe4648c989', 'rsi-overbought', 'RSI', 10, 'RSI overbought', 'RSI перекуплен', 'RSI en sobrecompra',
                 'RSI(14) enters the overbought zone or leaves it', 'RSI(14) входит в зону перекупленности или выходит из неё', 'El RSI(14) entra en sobrecompra o sale de ella',
                 $json${"id":null,"operator":"And","conditions":[{"id":null,"indicator":"RSI","templateData":{"Period":14,"Overbought":70,"Oversold":30,"Direction":"Up","TimeFrame":2},"operator":"GreaterThan","referenceType":"Value","referenceIndicator":null,"referenceTemplateData":null,"referenceCandlePart":null,"referenceValue":null,"deviation":null,"ticker":"","exchangeName":null,"timeFrame":2}],"groups":[]}$json$, CURRENT_TIMESTAMP),
                ('47e4e6eb-576b-7001-9d91-ddf9b8de6d15', 'rsi-oversold', 'RSI', 20, 'RSI oversold', 'RSI перепродан', 'RSI en sobreventa',
                 'RSI(14) enters the oversold zone or leaves it', 'RSI(14) входит в зону перепроданности или выходит из неё', 'El RSI(14) entra en sobreventa o sale de ella',
                 $json${"id":null,"operator":"And","conditions":[{"id":null,"indicator":"RSI","templateData":{"Period":14,"Overbought":70,"Oversold":30,"Direction":"Down","TimeFrame":2},"operator":"GreaterThan","referenceType":"Value","referenceIndicator":null,"referenceTemplateData":null,"referenceCandlePart":null,"referenceValue":null,"deviation":null,"ticker":"","exchangeName":null,"timeFrame":2}],"groups":[]}$json$, CURRENT_TIMESTAMP),
                ('5fcb6abc-62fd-7731-9ee2-269b59f2c315', 'macd-bullish-cross', 'MACD', 30, 'MACD bullish cross', 'MACD: бычий кросс', 'MACD cruce alcista',
                 'MACD crosses above the signal line or the histogram turns positive', 'MACD пересекает сигнальную линию вверх или гистограмма уходит в плюс', 'MACD cruza la línea de señal al alza o el histograma pasa a positivo',
                 $json${"id":null,"operator":"And","conditions":[{"id":null,"indicator":"MACD","templateData":{"FastPeriods":12,"SlowPeriods":26,"SignalPeriods":9,"Direction":"Up","TimeFrame":2},"operator":"GreaterThan","referenceType":"Value","referenceIndicator":null,"referenceTemplateData":null,"referenceCandlePart":null,"referenceValue":null,"deviation":null,"ticker":"","exchangeName":null,"timeFrame":2}],"groups":[]}$json$, CURRENT_TIMESTAMP),
                ('4eed174f-7c30-7aa3-830f-60021933ce63', 'macd-bearish-cross', 'MACD', 40, 'MACD bearish cross', 'MACD: медвежий кросс', 'MACD cruce bajista',
                 'MACD crosses below the signal line or the histogram turns negative', 'MACD пересекает сигнальную линию вниз или гистограмма уходит в минус', 'MACD cruza la línea de señal a la baja o el histograma pasa a negativo',
                 $json${"id":null,"operator":"And","conditions":[{"id":null,"indicator":"MACD","templateData":{"FastPeriods":12,"SlowPeriods":26,"SignalPeriods":9,"Direction":"Down","TimeFrame":2},"operator":"GreaterThan","referenceType":"Value","referenceIndicator":null,"referenceTemplateData":null,"referenceCandlePart":null,"referenceValue":null,"deviation":null,"ticker":"","exchangeName":null,"timeFrame":2}],"groups":[]}$json$, CURRENT_TIMESTAMP),
                ('c12fd838-88cb-75d2-9e4c-dcdf746762ad', 'bb-upper-breakout', 'BollingerBands', 50, 'BB upper breakout', 'Пробой верхней полосы BB', 'Ruptura de la banda superior BB',
                 'Price leaves the bands upward or returns from above the upper band', 'Цена выходит за полосы вверх или возвращается из-за верхней полосы', 'El precio sale de las bandas por arriba o vuelve desde la banda superior',
                 $json${"id":null,"operator":"And","conditions":[{"id":null,"indicator":"BollingerBands","templateData":{"Period":20,"StandardDeviation":2,"CandlePart":"Close","Direction":"Up","TimeFrame":2},"operator":"GreaterThan","referenceType":"Value","referenceIndicator":null,"referenceTemplateData":null,"referenceCandlePart":null,"referenceValue":null,"deviation":null,"ticker":"","exchangeName":null,"timeFrame":2}],"groups":[]}$json$, CURRENT_TIMESTAMP),
                ('bac8aa4e-1fcc-73b3-8dee-c3cdf14aefb1', 'bb-lower-breakout', 'BollingerBands', 60, 'BB lower breakout', 'Пробой нижней полосы BB', 'Ruptura de la banda inferior BB',
                 'Price leaves the bands downward or returns from below the lower band', 'Цена выходит за полосы вниз или возвращается из-за нижней полосы', 'El precio sale de las bandas por abajo o vuelve desde la banda inferior',
                 $json${"id":null,"operator":"And","conditions":[{"id":null,"indicator":"BollingerBands","templateData":{"Period":20,"StandardDeviation":2,"CandlePart":"Close","Direction":"Down","TimeFrame":2},"operator":"GreaterThan","referenceType":"Value","referenceIndicator":null,"referenceTemplateData":null,"referenceCandlePart":null,"referenceValue":null,"deviation":null,"ticker":"","exchangeName":null,"timeFrame":2}],"groups":[]}$json$, CURRENT_TIMESTAMP),
                ('f97120f9-a6ad-7b9e-b04c-c1a6c73eed8d', 'volume-spike-2x', 'Volume', 70, 'Volume spike x2', 'Всплеск объёма x2', 'Pico de volumen x2',
                 'Volume of the last candle is at least 2x the average of 20 candles', 'Объём последней свечи минимум в 2 раза выше среднего за 20 свечей', 'El volumen de la última vela duplica el promedio de 20 velas',
                 $json${"id":null,"operator":"And","conditions":[{"id":null,"indicator":"Volume","templateData":{"Periods":20,"Multiplier":2,"TimeFrame":2},"operator":"GreaterThan","referenceType":"Value","referenceIndicator":null,"referenceTemplateData":null,"referenceCandlePart":null,"referenceValue":null,"deviation":null,"ticker":"","exchangeName":null,"timeFrame":2}],"groups":[]}$json$, CURRENT_TIMESTAMP),
                ('7477e711-cd4b-708c-b8b6-42834ae32b88', 'gap-up', 'Gap', 80, 'Gap up 2%', 'Гэп вверх 2%', 'Gap al alza 2%',
                 'Open is at least 2% above the previous close', 'Открытие минимум на 2% выше предыдущего закрытия', 'La apertura sube al menos un 2% sobre el cierre anterior',
                 $json${"id":null,"operator":"And","conditions":[{"id":null,"indicator":"Gap","templateData":{"Percent":2,"Direction":"Up","TimeFrame":2},"operator":"GreaterThan","referenceType":"Value","referenceIndicator":null,"referenceTemplateData":null,"referenceCandlePart":null,"referenceValue":null,"deviation":null,"ticker":"","exchangeName":null,"timeFrame":2}],"groups":[]}$json$, CURRENT_TIMESTAMP),
                ('f7a9ba51-a7db-79be-acaf-0789e2e76e0d', 'gap-down', 'Gap', 90, 'Gap down 2%', 'Гэп вниз 2%', 'Gap a la baja 2%',
                 'Open is at least 2% below the previous close', 'Открытие минимум на 2% ниже предыдущего закрытия', 'La apertura cae al menos un 2% bajo el cierre anterior',
                 $json${"id":null,"operator":"And","conditions":[{"id":null,"indicator":"Gap","templateData":{"Percent":2,"Direction":"Down","TimeFrame":2},"operator":"GreaterThan","referenceType":"Value","referenceIndicator":null,"referenceTemplateData":null,"referenceCandlePart":null,"referenceValue":null,"deviation":null,"ticker":"","exchangeName":null,"timeFrame":2}],"groups":[]}$json$, CURRENT_TIMESTAMP),
                ('5656e9af-b709-7e08-b725-bb0f38548dc1', 'rsi-oversold-volume-spike', 'RSI', 100, 'RSI oversold + volume x2', 'RSI перепродан + объём x2', 'RSI sobreventa + volumen x2',
                 'Momentum bottom together with a volume surge (premium combo)', 'Дно импульса вместе со всплеском объёма (премиум-связка)', 'Mínimo de momentum junto a un pico de volumen (combo premium)',
                 $json${"id":null,"operator":"And","conditions":[{"id":null,"indicator":"RSI","templateData":{"Period":14,"Overbought":70,"Oversold":30,"Direction":"Down","TimeFrame":2},"operator":"GreaterThan","referenceType":"Value","referenceIndicator":null,"referenceTemplateData":null,"referenceCandlePart":null,"referenceValue":null,"deviation":null,"ticker":"","exchangeName":null,"timeFrame":2},{"id":null,"indicator":"Volume","templateData":{"Periods":20,"Multiplier":2,"TimeFrame":2},"operator":"GreaterThan","referenceType":"Value","referenceIndicator":null,"referenceTemplateData":null,"referenceCandlePart":null,"referenceValue":null,"deviation":null,"ticker":"","exchangeName":null,"timeFrame":2}],"groups":[]}$json$, CURRENT_TIMESTAMP);
                
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "RulePresets" ("Id", "Key", "Indicator", "Order", "NameEn", "NameRu", "NameEs", "DescriptionEn", "DescriptionRu", "DescriptionEs", "RuleData", "Created") VALUES
                ('64ea03d4-a966-409e-97b5-72cea8dd2f2e', 'openchange-down-3', 'OpenChange', 110, 'Down 3% from open', 'Падение 3% от открытия дня', 'Caída 3% desde la apertura',
                 'Close is at least 3% below the opening price of the day', 'Закрытие минимум на 3% ниже цены открытия дня', 'El cierre cae al menos un 3% bajo el precio de apertura del día',
                 $json${"id":null,"operator":"And","conditions":[{"id":null,"indicator":"OpenChange","templateData":{"Percent":3,"Direction":"Down","TimeFrame":2},"operator":"GreaterThan","referenceType":"Value","referenceIndicator":null,"referenceTemplateData":null,"referenceCandlePart":null,"referenceValue":null,"deviation":null,"ticker":"","exchangeName":null,"timeFrame":2}],"groups":[]}$json$, CURRENT_TIMESTAMP),
                ('cdfa9555-cd4c-40d1-9195-c5d0f8327c93', 'openchange-up-3', 'OpenChange', 120, 'Up 3% from open', 'Рост 3% от открытия дня', 'Subida 3% desde la apertura',
                 'Close is at least 3% above the opening price of the day', 'Закрытие минимум на 3% выше цены открытия дня', 'El cierre sube al menos un 3% sobre el precio de apertura del día',
                 $json${"id":null,"operator":"And","conditions":[{"id":null,"indicator":"OpenChange","templateData":{"Percent":3,"Direction":"Up","TimeFrame":2},"operator":"GreaterThan","referenceType":"Value","referenceIndicator":null,"referenceTemplateData":null,"referenceCandlePart":null,"referenceValue":null,"deviation":null,"ticker":"","exchangeName":null,"timeFrame":2}],"groups":[]}$json$, CURRENT_TIMESTAMP),
                ('683866e8-abb0-4eab-9e11-f6fbbec640a7', 'week52-new-high', 'Week52HighLow', 130, 'New 52-week high', 'Новый 52-недельный хай', 'Nuevo máximo de 52 semanas',
                 'The close steps above the highest high of the previous 52 weeks', 'Закрытие поднимается выше максимума предыдущих 52 недель', 'El cierre sube por encima del máximo de las 52 semanas anteriores',
                 $json${"id":null,"operator":"And","conditions":[{"id":null,"indicator":"Week52HighLow","templateData":{"Weeks":52,"Extreme":"High","Mode":"NewExtreme","PullbackPercent":5,"TimeFrame":2},"operator":"GreaterThan","referenceType":"Value","referenceIndicator":null,"referenceTemplateData":null,"referenceCandlePart":null,"referenceValue":null,"deviation":null,"ticker":"","exchangeName":null,"timeFrame":2}],"groups":[]}$json$, CURRENT_TIMESTAMP),
                ('f01b3138-7107-422c-b059-e450f630310e', 'week52-new-low', 'Week52HighLow', 140, 'New 52-week low', 'Новый 52-недельный лоу', 'Nuevo mínimo de 52 semanas',
                 'The close drops below the lowest low of the previous 52 weeks', 'Закрытие опускается ниже минимума предыдущих 52 недель', 'El cierre baja por debajo del mínimo de las 52 semanas anteriores',
                 $json${"id":null,"operator":"And","conditions":[{"id":null,"indicator":"Week52HighLow","templateData":{"Weeks":52,"Extreme":"Low","Mode":"NewExtreme","PullbackPercent":5,"TimeFrame":2},"operator":"GreaterThan","referenceType":"Value","referenceIndicator":null,"referenceTemplateData":null,"referenceCandlePart":null,"referenceValue":null,"deviation":null,"ticker":"","exchangeName":null,"timeFrame":2}],"groups":[]}$json$, CURRENT_TIMESTAMP),
                ('94c056be-c4bc-417d-beef-9b51b14e1146', 'trailing-stop-long-5', 'TrailingStop', 150, 'Long trailing stop -5%', 'Трейл-стоп лонга: −5%', 'Stop dinámico del largo: -5%',
                 'Alert when the close falls 5% under the highest high since the alert was armed', 'Алерт, когда закрытие падает на 5% ниже максимума с момента взведения алерта', 'Alerta cuando el cierre cae un 5% bajo el máximo desde que se armó la alerta',
                 $json${"id":null,"operator":"And","conditions":[{"id":null,"indicator":"TrailingStop","templateData":{"Type":"Long","ThresholdType":"Percent","Value":5,"TimeFrame":2},"operator":"GreaterThan","referenceType":"Value","referenceIndicator":null,"referenceTemplateData":null,"referenceCandlePart":null,"referenceValue":null,"deviation":null,"ticker":"","exchangeName":null,"timeFrame":2}],"groups":[]}$json$, CURRENT_TIMESTAMP);
                
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "RulePresets"
                """);
        }
    }
}
