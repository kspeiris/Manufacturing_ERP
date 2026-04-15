using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionBatchAndScrap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BatchNo",
                table: "ProductionOrders",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ScrapQuantity",
                table: "ProductionOrders",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatchNo",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "ScrapQuantity",
                table: "ProductionOrders");
        }
    }
}
