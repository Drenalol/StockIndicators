using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockIndicators.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedRulePresetFamilies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "RulePresets" p SET
                    "Family" = v."Family",
                    "FamilyLabelEn" = v."FamilyLabelEn",
                    "FamilyLabelRu" = v."FamilyLabelRu",
                    "FamilyLabelEs" = v."FamilyLabelEs",
                    "SegmentEn" = v."SegmentEn",
                    "SegmentRu" = v."SegmentRu",
                    "SegmentEs" = v."SegmentEs"
                FROM (VALUES
                    ('rsi-overbought', 'rsi-zones', 'RSI', 'RSI', 'RSI', 'Overbought', 'перекуплен', 'sobrecompra'),
                    ('rsi-oversold', 'rsi-zones', 'RSI', 'RSI', 'RSI', 'Oversold', 'перепродан', 'sobreventa'),
                    ('macd-bullish-cross', 'macd-cross', 'MACD', 'MACD', 'MACD', 'Bullish', 'бычий', 'alcista'),
                    ('macd-bearish-cross', 'macd-cross', 'MACD', 'MACD', 'MACD', 'Bearish', 'медвежий', 'bajista'),
                    ('bb-upper-breakout', 'bb-breakout', 'Bollinger', 'Полосы BB', 'Bandas BB', 'Upper', 'верхняя', 'superior'),
                    ('bb-lower-breakout', 'bb-breakout', 'Bollinger', 'Полосы BB', 'Bandas BB', 'Lower', 'нижняя', 'inferior'),
                    ('gap-up', 'gap-move', 'Gap', 'Гэп', 'Gap', 'Up', 'вверх', 'arriba'),
                    ('gap-down', 'gap-move', 'Gap', 'Гэп', 'Gap', 'Down', 'вниз', 'abajo'),
                    ('openchange-up-3', 'openchange-move', 'From open', 'Открытие', 'Apertura', '+3%', '+3%', '+3%'),
                    ('openchange-down-3', 'openchange-move', 'From open', 'Открытие', 'Apertura', '−3%', '−3%', '−3%'),
                    ('week52-new-high', 'week52-extremes', '52 weeks', '52 недель', '52 semanas', 'High', 'максимум', 'máximo'),
                    ('week52-new-low', 'week52-extremes', '52 weeks', '52 недель', '52 semanas', 'Low', 'минимум', 'mínimo')
                ) AS v("Key", "Family", "FamilyLabelEn", "FamilyLabelRu", "FamilyLabelEs", "SegmentEn", "SegmentRu", "SegmentEs")
                WHERE p."Key" = v."Key";

                -- The 52-week presets were named with the anglicisms "хай/лоу"; the
                -- Russian UI says "максимум/минимум" instead (keys and en/es untouched)
                UPDATE "RulePresets" SET "NameRu" = 'Новый 52-недельный максимум' WHERE "Key" = 'week52-new-high';
                UPDATE "RulePresets" SET "NameRu" = 'Новый 52-недельный минимум' WHERE "Key" = 'week52-new-low';
                
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
