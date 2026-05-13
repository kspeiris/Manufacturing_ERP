using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryReservationAndServiceSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BatchLots_WarehouseBins_WarehouseBinId",
                table: "BatchLots");

            migrationBuilder.DropIndex(
                name: "IX_StockBalances_ProductId",
                table: "StockBalances");

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityReserved",
                table: "StockBalances",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReorderLevel",
                table: "Products",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_StockBalances_ProductId_WarehouseId",
                table: "StockBalances",
                columns: new[] { "ProductId", "WarehouseId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BatchLots_WarehouseBins_WarehouseBinId",
                table: "BatchLots",
                column: "WarehouseBinId",
                principalTable: "WarehouseBins",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BatchLots_WarehouseBins_WarehouseBinId",
                table: "BatchLots");

            migrationBuilder.DropIndex(
                name: "IX_StockBalances_ProductId_WarehouseId",
                table: "StockBalances");

            migrationBuilder.DropColumn(
                name: "QuantityReserved",
                table: "StockBalances");

            migrationBuilder.DropColumn(
                name: "ReorderLevel",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_StockBalances_ProductId",
                table: "StockBalances",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_BatchLots_WarehouseBins_WarehouseBinId",
                table: "BatchLots",
                column: "WarehouseBinId",
                principalTable: "WarehouseBins",
                principalColumn: "Id");
        }
    }
}
