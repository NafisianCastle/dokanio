using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscountSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create Discounts table
            migrationBuilder.CreateTable(
                name: "Discounts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RequiredMembershipTier = table.Column<int>(type: "INTEGER", nullable: true),
                    MinimumQuantity = table.Column<int>(type: "INTEGER", nullable: true),
                    MinimumAmount = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: false),
                    ServerSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Discounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Discounts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // Create SaleDiscounts table
            migrationBuilder.CreateTable(
                name: "SaleDiscounts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SaleId = table.Column<string>(type: "TEXT", nullable: false),
                    DiscountId = table.Column<string>(type: "TEXT", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    DiscountReason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleDiscounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleDiscounts_Discounts_DiscountId",
                        column: x => x.DiscountId,
                        principalTable: "Discounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleDiscounts_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create indexes for Discounts table
            migrationBuilder.CreateIndex(
                name: "IX_Discounts_Category",
                table: "Discounts",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Discounts_DeviceId",
                table: "Discounts",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Discounts_EndDate",
                table: "Discounts",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_Discounts_IsActive",
                table: "Discounts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Discounts_IsDeleted",
                table: "Discounts",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Discounts_Name",
                table: "Discounts",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Discounts_ProductId",
                table: "Discounts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Discounts_RequiredMembershipTier",
                table: "Discounts",
                column: "RequiredMembershipTier");

            migrationBuilder.CreateIndex(
                name: "IX_Discounts_Scope",
                table: "Discounts",
                column: "Scope");

            migrationBuilder.CreateIndex(
                name: "IX_Discounts_StartDate",
                table: "Discounts",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_Discounts_SyncStatus",
                table: "Discounts",
                column: "SyncStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Discounts_Type",
                table: "Discounts",
                column: "Type");

            // Create indexes for SaleDiscounts table
            migrationBuilder.CreateIndex(
                name: "IX_SaleDiscounts_AppliedAt",
                table: "SaleDiscounts",
                column: "AppliedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SaleDiscounts_DiscountId",
                table: "SaleDiscounts",
                column: "DiscountId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleDiscounts_SaleId",
                table: "SaleDiscounts",
                column: "SaleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaleDiscounts");

            migrationBuilder.DropTable(
                name: "Discounts");
        }
    }
}