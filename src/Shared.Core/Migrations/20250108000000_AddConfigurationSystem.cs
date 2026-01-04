using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurationSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Configurations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsSystemLevel = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: false),
                    ServerSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_DeviceId",
                table: "Configurations",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_IsDeleted",
                table: "Configurations",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_IsSystemLevel",
                table: "Configurations",
                column: "IsSystemLevel");

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_Key",
                table: "Configurations",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_SyncStatus",
                table: "Configurations",
                column: "SyncStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_Type",
                table: "Configurations",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Configurations_UpdatedAt",
                table: "Configurations",
                column: "UpdatedAt");

            // Insert default system configurations
            migrationBuilder.InsertData(
                table: "Configurations",
                columns: new[] { "Id", "Key", "Value", "Type", "Description", "IsSystemLevel", "UpdatedAt", "DeviceId", "ServerSyncedAt", "SyncStatus", "IsDeleted", "DeletedAt" },
                values: new object[,]
                {
                    { Guid.NewGuid().ToString(), "Currency.Code", "USD", 0, "Currency code (ISO 4217)", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Currency.Symbol", "$", 0, "Currency symbol", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Currency.DecimalPlaces", "2", 1, "Number of decimal places for currency", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Currency.DecimalSeparator", ".", 0, "Decimal separator character", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Currency.ThousandsSeparator", ",", 0, "Thousands separator character", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Currency.SymbolBeforeAmount", "true", 2, "Whether to show currency symbol before amount", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Tax.Enabled", "true", 2, "Whether tax calculation is enabled", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Tax.DefaultRate", "0.0", 4, "Default tax rate percentage", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Tax.Name", "Tax", 0, "Display name for tax", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Tax.IncludedInPrice", "false", 2, "Whether tax is included in product prices", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Tax.ShowOnReceipt", "true", 2, "Whether to show tax details on receipt", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Business.Name", "", 0, "Business name", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Business.Address", "", 0, "Business address", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Business.Phone", "", 0, "Business phone number", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Business.Email", "", 0, "Business email address", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Business.Website", "", 0, "Business website URL", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Business.Logo", "", 0, "Business logo path or URL", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Business.ReceiptFooter", "Thank you for your business!", 0, "Footer text for receipts", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Localization.Language", "en", 0, "Application language code", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Localization.Country", "US", 0, "Country code", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Localization.TimeZone", "UTC", 0, "Time zone identifier", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Localization.DateFormat", "MM/dd/yyyy", 0, "Date format pattern", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Localization.TimeFormat", "HH:mm:ss", 0, "Time format pattern", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null },
                    { Guid.NewGuid().ToString(), "Localization.NumberFormat", "N2", 0, "Number format pattern", true, DateTime.UtcNow, Guid.Empty.ToString(), null, 0, false, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Configurations");
        }
    }
}