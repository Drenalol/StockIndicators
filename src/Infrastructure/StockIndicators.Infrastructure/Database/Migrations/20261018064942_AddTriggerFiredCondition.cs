using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockIndicators.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTriggerFiredCondition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FiredConditionData",
                table: "TriggerHistory",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FiredConditionData",
                table: "TriggerHistory");
        }
    }
}
