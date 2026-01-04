using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddWeightBasedPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add weight-based pricing columns to Products table
            migrationBuilder.AddColumn<bool>(
                name: "IsWeightBased",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RatePerKilogram",
                table: "Products",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeightPrecision",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 3);

            // Add weight-based pricing columns to SaleItems table
            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                table: "SaleItems",
                type: "TEXT",
                precision: 10,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RatePerKilogram",
                table: "SaleItems",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPrice",
                table: "SaleItems",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Create index on IsWeightBased for efficient queries
            migrationBuilder.CreateIndex(
                name: "IX_Products_IsWeightBased",
                table: "Products",
                column: "IsWeightBased");

            // Update existing SaleItems to have TotalPrice calculated from Quantity * UnitPrice
            migrationBuilder.Sql(@"
                UPDATE SaleItems 
                SET TotalPrice = Quantity * UnitPrice 
                WHERE TotalPrice = 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop index
            migrationBuilder.DropIndex(
                name: "IX_Products_IsWeightBased",
                table: "Products");

            // Remove weight-based pricing columns from Products table
            migrationBuilder.DropColumn(
                name: "IsWeightBased",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RatePerKilogram",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "WeightPrecision",
                table: "Products");

            // Remove weight-based pricing columns from SaleItems table
            migrationBuilder.DropColumn(
                name: "Weight",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "RatePerKilogram",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "TotalPrice",
                table: "SaleItems");
        }
    }
}