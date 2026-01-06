using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddShopAndUserConfigurationSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add ShopId and UserId columns to Configuration table
            migrationBuilder.AddColumn<Guid>(
                name: "ShopId",
                table: "Configurations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Configurations",
                type: "uniqueidentifier",
                nullable: true);

            // Create indexes for better performance
            migrationBuilder.CreateIndex(
                name: "IX_Configurations_ShopId_Key",
                table: "Configurations",
                columns: new[] { "ShopId", "Key" },
                unique: true,
                filter: "[ShopId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_UserId_Key",
                table: "Configurations",
                columns: new[] { "UserId", "Key" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_ShopId",
                table: "Configurations",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_UserId",
                table: "Configurations",
                column: "UserId");

            // Add foreign key constraints
            migrationBuilder.AddForeignKey(
                name: "FK_Configurations_Shops_ShopId",
                table: "Configurations",
                column: "ShopId",
                principalTable: "Shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Configurations_Users_UserId",
                table: "Configurations",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop foreign key constraints
            migrationBuilder.DropForeignKey(
                name: "FK_Configurations_Shops_ShopId",
                table: "Configurations");

            migrationBuilder.DropForeignKey(
                name: "FK_Configurations_Users_UserId",
                table: "Configurations");

            // Drop indexes
            migrationBuilder.DropIndex(
                name: "IX_Configurations_ShopId_Key",
                table: "Configurations");

            migrationBuilder.DropIndex(
                name: "IX_Configurations_UserId_Key",
                table: "Configurations");

            migrationBuilder.DropIndex(
                name: "IX_Configurations_ShopId",
                table: "Configurations");

            migrationBuilder.DropIndex(
                name: "IX_Configurations_UserId",
                table: "Configurations");

            // Drop columns
            migrationBuilder.DropColumn(
                name: "ShopId",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Configurations");
        }
    }
}