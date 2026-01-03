using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionLogging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransactionLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Operation = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "TEXT", nullable: false),
                    EntityData = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsProcessed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLogs_CreatedAt",
                table: "TransactionLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLogs_DeviceId",
                table: "TransactionLogs",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLogs_EntityId",
                table: "TransactionLogs",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLogs_EntityType",
                table: "TransactionLogs",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLogs_IsProcessed",
                table: "TransactionLogs",
                column: "IsProcessed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionLogs");
        }
    }
}