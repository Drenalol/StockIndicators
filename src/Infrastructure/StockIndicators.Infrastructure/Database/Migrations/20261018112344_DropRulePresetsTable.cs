using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockIndicators.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropRulePresetsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The preset catalog is product content and moved into the assembly
            // (Rules/Data/rule-presets.json, read by RulePresetCatalog): the table only ever
            // carried rows these migrations seeded, so dropping it loses nothing a user made.
            migrationBuilder.DropTable(
                name: "RulePresets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RulePresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DescriptionBe = table.Column<string>(type: "varchar(300)", nullable: true),
                    DescriptionDe = table.Column<string>(type: "varchar(300)", nullable: true),
                    DescriptionEn = table.Column<string>(type: "varchar(300)", nullable: true),
                    DescriptionEs = table.Column<string>(type: "varchar(300)", nullable: true),
                    DescriptionFr = table.Column<string>(type: "varchar(300)", nullable: true),
                    DescriptionIt = table.Column<string>(type: "varchar(300)", nullable: true),
                    DescriptionPtBr = table.Column<string>(type: "varchar(300)", nullable: true),
                    DescriptionRu = table.Column<string>(type: "varchar(300)", nullable: true),
                    DescriptionUk = table.Column<string>(type: "varchar(300)", nullable: true),
                    Family = table.Column<string>(type: "varchar(40)", nullable: true),
                    FamilyLabelBe = table.Column<string>(type: "varchar(40)", nullable: true),
                    FamilyLabelDe = table.Column<string>(type: "varchar(40)", nullable: true),
                    FamilyLabelEn = table.Column<string>(type: "varchar(40)", nullable: true),
                    FamilyLabelEs = table.Column<string>(type: "varchar(40)", nullable: true),
                    FamilyLabelFr = table.Column<string>(type: "varchar(40)", nullable: true),
                    FamilyLabelIt = table.Column<string>(type: "varchar(40)", nullable: true),
                    FamilyLabelPtBr = table.Column<string>(type: "varchar(40)", nullable: true),
                    FamilyLabelRu = table.Column<string>(type: "varchar(40)", nullable: true),
                    FamilyLabelUk = table.Column<string>(type: "varchar(40)", nullable: true),
                    Indicator = table.Column<string>(type: "varchar(40)", nullable: false),
                    Key = table.Column<string>(type: "varchar(60)", nullable: false),
                    NameBe = table.Column<string>(type: "varchar(100)", nullable: true),
                    NameDe = table.Column<string>(type: "varchar(100)", nullable: true),
                    NameEn = table.Column<string>(type: "varchar(100)", nullable: false),
                    NameEs = table.Column<string>(type: "varchar(100)", nullable: false),
                    NameFr = table.Column<string>(type: "varchar(100)", nullable: true),
                    NameIt = table.Column<string>(type: "varchar(100)", nullable: true),
                    NamePtBr = table.Column<string>(type: "varchar(100)", nullable: true),
                    NameRu = table.Column<string>(type: "varchar(100)", nullable: false),
                    NameUk = table.Column<string>(type: "varchar(100)", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    RuleData = table.Column<string>(type: "jsonb", nullable: false),
                    SegmentBe = table.Column<string>(type: "varchar(40)", nullable: true),
                    SegmentDe = table.Column<string>(type: "varchar(40)", nullable: true),
                    SegmentEn = table.Column<string>(type: "varchar(40)", nullable: true),
                    SegmentEs = table.Column<string>(type: "varchar(40)", nullable: true),
                    SegmentFr = table.Column<string>(type: "varchar(40)", nullable: true),
                    SegmentIt = table.Column<string>(type: "varchar(40)", nullable: true),
                    SegmentPtBr = table.Column<string>(type: "varchar(40)", nullable: true),
                    SegmentRu = table.Column<string>(type: "varchar(40)", nullable: true),
                    SegmentUk = table.Column<string>(type: "varchar(40)", nullable: true),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RulePresets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RulePresets_Key",
                table: "RulePresets",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RulePresets_Order",
                table: "RulePresets",
                column: "Order");
        }
    }
}
