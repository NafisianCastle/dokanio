using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Core.Migrations
{
    /// <inheritdoc />
    public partial class SeedBusinessTypeConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Insert default business type configurations
            migrationBuilder.InsertData(
                table: "Configurations",
                columns: new[] { "Id", "Key", "Value", "Type", "Description", "IsSystemLevel", "UpdatedAt", "DeviceId", "ServerSyncedAt", "SyncStatus", "IsDeleted", "DeletedAt" },
                values: new object[,]
                {
                    // Grocery Business Type Configuration
                    { Guid.NewGuid(), "BusinessType.Grocery.WeightBasedProducts", "true", 2, "Enable weight-based products for grocery stores", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "BusinessType.Grocery.DefaultWeightUnit", "kg", 0, "Default weight unit for grocery products", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "BusinessType.Grocery.WeightPrecision", "3", 1, "Decimal places for weight measurements", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "BusinessType.Grocery.RequiredAttributes", "[\"Weight\", \"Volume\", \"Category\"]", 3, "Required product attributes for grocery stores", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    
                    // SuperShop Business Type Configuration
                    { Guid.NewGuid(), "BusinessType.SuperShop.CategoryManagement", "true", 2, "Enable advanced category management for super shops", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "BusinessType.SuperShop.BulkDiscounts", "true", 2, "Enable bulk discount features", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "BusinessType.SuperShop.RequiredAttributes", "[\"Category\", \"Brand\", \"Barcode\"]", 3, "Required product attributes for super shops", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    
                    // Pharmacy Business Type Configuration
                    { Guid.NewGuid(), "BusinessType.Pharmacy.ExpiryTracking", "true", 2, "Enable expiry date tracking for pharmacy products", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "BusinessType.Pharmacy.ExpiryWarningDays", "30", 1, "Days before expiry to show warnings", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "BusinessType.Pharmacy.BatchTracking", "true", 2, "Enable batch number tracking", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "BusinessType.Pharmacy.RequiredAttributes", "[\"ExpiryDate\", \"BatchNumber\", \"Manufacturer\", \"Dosage\"]", 3, "Required product attributes for pharmacies", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "BusinessType.Pharmacy.PreventExpiredSales", "true", 2, "Prevent sales of expired products", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    
                    // General Retail Business Type Configuration
                    { Guid.NewGuid(), "BusinessType.GeneralRetail.BasicFeatures", "true", 2, "Enable basic retail features", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "BusinessType.GeneralRetail.RequiredAttributes", "[\"Category\", \"Brand\"]", 3, "Required product attributes for general retail", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    
                    // Custom Business Type Configuration
                    { Guid.NewGuid(), "BusinessType.Custom.AllowCustomAttributes", "true", 2, "Allow custom product attributes", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "BusinessType.Custom.RequiredAttributes", "[]", 3, "Required product attributes for custom business types", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    
                    // Multi-Tenant Configuration
                    { Guid.NewGuid(), "MultiTenant.DataIsolation", "true", 2, "Enable strict data isolation between businesses", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "MultiTenant.MaxShopsPerBusiness", "10", 1, "Maximum number of shops per business", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "MultiTenant.MaxUsersPerBusiness", "50", 1, "Maximum number of users per business", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    
                    // Performance Optimization Configuration
                    { Guid.NewGuid(), "Performance.DatabaseIndexing", "true", 2, "Enable database indexing optimizations", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "Performance.QueryCaching", "true", 2, "Enable query result caching", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "Performance.BatchSize", "100", 1, "Default batch size for bulk operations", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    
                    // Security Configuration
                    { Guid.NewGuid(), "Security.DataEncryption", "true", 2, "Enable data encryption at rest", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "Security.TransmissionEncryption", "true", 2, "Enable data encryption in transit", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "Security.SessionTimeout", "480", 1, "Session timeout in minutes (8 hours)", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "Security.PasswordMinLength", "8", 1, "Minimum password length", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "Security.RequirePasswordComplexity", "true", 2, "Require complex passwords", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    
                    // Synchronization Configuration
                    { Guid.NewGuid(), "Sync.AutoSyncInterval", "300", 1, "Auto sync interval in seconds (5 minutes)", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "Sync.ConflictResolution", "LastWriteWins", 0, "Default conflict resolution strategy", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "Sync.MaxRetryAttempts", "3", 1, "Maximum sync retry attempts", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    
                    // AI Analytics Configuration
                    { Guid.NewGuid(), "AI.EnableRecommendations", "true", 2, "Enable AI-powered recommendations", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "AI.MinDataPointsForPrediction", "30", 1, "Minimum data points required for AI predictions", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "AI.RecommendationRefreshInterval", "3600", 1, "Recommendation refresh interval in seconds (1 hour)", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    
                    // Reporting Configuration
                    { Guid.NewGuid(), "Reports.DefaultFormat", "PDF", 0, "Default report export format", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "Reports.MaxRecordsPerReport", "10000", 1, "Maximum records per report", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null },
                    { Guid.NewGuid(), "Reports.AutoGenerateDaily", "true", 2, "Auto-generate daily reports", true, DateTime.UtcNow, Guid.Empty, null, 0, false, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove business type configurations
            migrationBuilder.Sql("DELETE FROM Configurations WHERE Key LIKE 'BusinessType.%';");
            migrationBuilder.Sql("DELETE FROM Configurations WHERE Key LIKE 'MultiTenant.%';");
            migrationBuilder.Sql("DELETE FROM Configurations WHERE Key LIKE 'Performance.%';");
            migrationBuilder.Sql("DELETE FROM Configurations WHERE Key LIKE 'Security.%';");
            migrationBuilder.Sql("DELETE FROM Configurations WHERE Key LIKE 'Sync.%';");
            migrationBuilder.Sql("DELETE FROM Configurations WHERE Key LIKE 'AI.%';");
            migrationBuilder.Sql("DELETE FROM Configurations WHERE Key LIKE 'Reports.%';");
        }
    }
}
