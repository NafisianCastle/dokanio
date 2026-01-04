using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerMembershipSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create Customers table
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    MembershipNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    JoinDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Tier = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalSpent = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    VisitCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastVisit = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: false),
                    ServerSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            // Add customer-related columns to Sales table
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Sales",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "Sales",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MembershipDiscountAmount",
                table: "Sales",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CustomerId",
                table: "Sales",
                type: "TEXT",
                nullable: true);

            // Create indexes for Customers table
            migrationBuilder.CreateIndex(
                name: "IX_Customers_MembershipNumber",
                table: "Customers",
                column: "MembershipNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Tier",
                table: "Customers",
                column: "Tier");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_IsActive",
                table: "Customers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_JoinDate",
                table: "Customers",
                column: "JoinDate");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TotalSpent",
                table: "Customers",
                column: "TotalSpent");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_SyncStatus",
                table: "Customers",
                column: "SyncStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_DeviceId",
                table: "Customers",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_IsDeleted",
                table: "Customers",
                column: "IsDeleted");

            // Create index for Sales.CustomerId
            migrationBuilder.CreateIndex(
                name: "IX_Sales_CustomerId",
                table: "Sales",
                column: "CustomerId");

            // Create foreign key relationship
            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Customers_CustomerId",
                table: "Sales",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop foreign key
            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Customers_CustomerId",
                table: "Sales");

            // Drop indexes
            migrationBuilder.DropIndex(
                name: "IX_Sales_CustomerId",
                table: "Sales");

            // Drop customer-related columns from Sales table
            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "MembershipDiscountAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Sales");

            // Drop Customers table
            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}