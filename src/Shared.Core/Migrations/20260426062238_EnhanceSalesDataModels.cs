using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Core.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceSalesDataModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Shops_ShopId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_SaleDiscounts_DiscountId",
                table: "SaleDiscounts");

            migrationBuilder.DropIndex(
                name: "IX_Products_ShopId_Barcode",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "DiscountAmount",
                table: "SaleDiscounts",
                newName: "CalculatedAmount");

            migrationBuilder.RenameIndex(
                name: "IX_SaleDiscounts_SaleId",
                table: "SaleDiscounts",
                newName: "IX_SaleDiscount_SaleId");

            migrationBuilder.RenameIndex(
                name: "IX_SaleDiscounts_AppliedAt",
                table: "SaleDiscounts",
                newName: "IX_SaleDiscount_AppliedAt");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Stock",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaid",
                table: "Sales",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Sales",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Sales",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ChangeAmount",
                table: "Sales",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Sales",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalTotal",
                table: "Sales",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModified",
                table: "Sales",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "Sales",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalDiscount",
                table: "Sales",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalTax",
                table: "Sales",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Sales",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "SaleItems",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                table: "SaleItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsWeightBased",
                table: "SaleItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LineDiscount",
                table: "SaleItems",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LineSubtotal",
                table: "SaleItems",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LineTax",
                table: "SaleItems",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LineTotal",
                table: "SaleItems",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ProductCode",
                table: "SaleItems",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductName",
                table: "SaleItems",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "DiscountReason",
                table: "SaleDiscounts",
                type: "TEXT",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<Guid>(
                name: "DiscountId",
                table: "SaleDiscounts",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<Guid>(
                name: "AuthorizedBy",
                table: "SaleDiscounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "SaleDiscounts",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "SaleDiscounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeviceId",
                table: "SaleDiscounts",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "DiscountName",
                table: "SaleDiscounts",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DiscountType",
                table: "SaleDiscounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "FixedAmount",
                table: "SaleDiscounts",
                type: "TEXT",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "SaleDiscounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PercentageValue",
                table: "SaleDiscounts",
                type: "TEXT",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ServerSyncedAt",
                table: "SaleDiscounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SyncStatus",
                table: "SaleDiscounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "SaleDiscounts",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "MaxWeightKg",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinWeightKg",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ShopId",
                table: "Configurations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Configurations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Tier = table.Column<int>(type: "INTEGER", nullable: false),
                    JoinDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DiscountPercentage = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalSpentForTier = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerMemberships_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerPreferences_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SaleAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SaleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    EventDescription = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    OldValues = table.Column<string>(type: "TEXT", nullable: true),
                    NewValues = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaleItemDiscounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SaleItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SaleDiscountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleItemDiscounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleItemDiscounts_SaleDiscounts_SaleDiscountId",
                        column: x => x.SaleDiscountId,
                        principalTable: "SaleDiscounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SaleItemDiscounts_SaleItems_SaleItemId",
                        column: x => x.SaleItemId,
                        principalTable: "SaleItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalePayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SaleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PaymentMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    AmountTendered = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    ChangeAmount = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FailureReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalePayments_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SaleSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TabName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ShopId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PaymentMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionData = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    SaleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleSessions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SaleSessions_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SaleSessions_Shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "Shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SaleSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ContactPerson = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MembershipBenefits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerMembershipId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MaxUsages = table.Column<int>(type: "INTEGER", nullable: true),
                    UsageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipBenefits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MembershipBenefits_CustomerMemberships_CustomerMembershipId",
                        column: x => x.CustomerMembershipId,
                        principalTable: "CustomerMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLog_Device_IsProcessed",
                table: "TransactionLogs",
                columns: new[] { "DeviceId", "IsProcessed" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLog_Entity_CreatedAt",
                table: "TransactionLogs",
                columns: new[] { "EntityType", "EntityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLog_IsProcessed_CreatedAt",
                table: "TransactionLogs",
                columns: new[] { "IsProcessed", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sale_Created_Amount",
                table: "Sales",
                columns: new[] { "CreatedAt", "TotalAmount" });

            migrationBuilder.CreateIndex(
                name: "IX_Sale_Customer_Created",
                table: "Sales",
                columns: new[] { "CustomerId", "CreatedAt" },
                filter: "CustomerId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sale_Shop_Created_NotDeleted",
                table: "Sales",
                columns: new[] { "ShopId", "CreatedAt", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Sale_Shop_Payment_Created",
                table: "Sales",
                columns: new[] { "ShopId", "PaymentMethod", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sale_Status",
                table: "Sales",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Sale_User_Created",
                table: "Sales",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleItem_BatchNumber",
                table: "SaleItems",
                column: "BatchNumber",
                filter: "BatchNumber IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SaleDiscount_DiscountId",
                table: "SaleDiscounts",
                column: "DiscountId",
                filter: "DiscountId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SaleDiscount_DiscountType",
                table: "SaleDiscounts",
                column: "DiscountType");

            migrationBuilder.CreateIndex(
                name: "IX_SaleDiscounts_IsDeleted",
                table: "SaleDiscounts",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Product_Barcode_Shop",
                table: "Products",
                columns: new[] { "Barcode", "ShopId" });

            migrationBuilder.CreateIndex(
                name: "IX_Product_Expiry_Shop",
                table: "Products",
                columns: new[] { "ExpiryDate", "ShopId" },
                filter: "ExpiryDate IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Product_Name_Shop",
                table: "Products",
                columns: new[] { "Name", "ShopId" });

            migrationBuilder.CreateIndex(
                name: "IX_Product_Shop_Active_NotDeleted",
                table: "Products",
                columns: new[] { "ShopId", "IsActive", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Product_Shop_Category_Active",
                table: "Products",
                columns: new[] { "ShopId", "Category", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_SupplierId",
                table: "Products",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Customer_Phone",
                table: "Customers",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_Customer_Phone_Active_NotDeleted",
                table: "Customers",
                columns: new[] { "Phone", "IsActive", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Customer_Spent_Tier",
                table: "Customers",
                columns: new[] { "TotalSpent", "Tier" });

            migrationBuilder.CreateIndex(
                name: "IX_Customer_Tier_Active",
                table: "Customers",
                columns: new[] { "Tier", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Business_Owner_Active_NotDeleted",
                table: "Businesses",
                columns: new[] { "OwnerId", "IsActive", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Business_Sync_Updated",
                table: "Businesses",
                columns: new[] { "SyncStatus", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Business_Type_Active",
                table: "Businesses",
                columns: new[] { "Type", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMembership_Customer_Active_NotDeleted",
                table: "CustomerMemberships",
                columns: new[] { "CustomerId", "IsActive", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMembership_Expiry_Active",
                table: "CustomerMemberships",
                columns: new[] { "ExpiryDate", "IsActive" },
                filter: "ExpiryDate IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMembership_Tier_Active",
                table: "CustomerMemberships",
                columns: new[] { "Tier", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMemberships_CustomerId",
                table: "CustomerMemberships",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMemberships_DeviceId",
                table: "CustomerMemberships",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMemberships_ExpiryDate",
                table: "CustomerMemberships",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMemberships_IsActive",
                table: "CustomerMemberships",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMemberships_IsDeleted",
                table: "CustomerMemberships",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMemberships_JoinDate",
                table: "CustomerMemberships",
                column: "JoinDate");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMemberships_SyncStatus",
                table: "CustomerMemberships",
                column: "SyncStatus");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMemberships_Tier",
                table: "CustomerMemberships",
                column: "Tier");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPreference_Customer_Category_Active",
                table: "CustomerPreferences",
                columns: new[] { "CustomerId", "Category", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPreference_Customer_Key",
                table: "CustomerPreferences",
                columns: new[] { "CustomerId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPreferences_Category",
                table: "CustomerPreferences",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPreferences_CustomerId",
                table: "CustomerPreferences",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPreferences_DeviceId",
                table: "CustomerPreferences",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPreferences_IsActive",
                table: "CustomerPreferences",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPreferences_IsDeleted",
                table: "CustomerPreferences",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPreferences_Key",
                table: "CustomerPreferences",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPreferences_SyncStatus",
                table: "CustomerPreferences",
                column: "SyncStatus");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipBenefit_DateRange_Active",
                table: "MembershipBenefits",
                columns: new[] { "StartDate", "EndDate", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipBenefit_Membership_Active_NotDeleted",
                table: "MembershipBenefits",
                columns: new[] { "CustomerMembershipId", "IsActive", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipBenefit_Type_Active",
                table: "MembershipBenefits",
                columns: new[] { "Type", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipBenefits_CustomerMembershipId",
                table: "MembershipBenefits",
                column: "CustomerMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipBenefits_DeviceId",
                table: "MembershipBenefits",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipBenefits_EndDate",
                table: "MembershipBenefits",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipBenefits_IsActive",
                table: "MembershipBenefits",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipBenefits_IsDeleted",
                table: "MembershipBenefits",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipBenefits_StartDate",
                table: "MembershipBenefits",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipBenefits_SyncStatus",
                table: "MembershipBenefits",
                column: "SyncStatus");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipBenefits_Type",
                table: "MembershipBenefits",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_SaleAuditLog_Sale_Timestamp",
                table: "SaleAuditLogs",
                columns: new[] { "SaleId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleAuditLog_SaleId",
                table: "SaleAuditLogs",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleAuditLog_Timestamp",
                table: "SaleAuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_SaleAuditLog_User_Timestamp",
                table: "SaleAuditLogs",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_SaleAuditLog_UserId",
                table: "SaleAuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleAuditLogs_IsDeleted",
                table: "SaleAuditLogs",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItemDiscount_SaleDiscountId",
                table: "SaleItemDiscounts",
                column: "SaleDiscountId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItemDiscount_SaleItemId",
                table: "SaleItemDiscounts",
                column: "SaleItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItemDiscount_SaleItemId_SaleDiscountId",
                table: "SaleItemDiscounts",
                columns: new[] { "SaleItemId", "SaleDiscountId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalePayment_SaleId",
                table: "SalePayments",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_SalePayment_SaleId_Status",
                table: "SalePayments",
                columns: new[] { "SaleId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SalePayment_Status",
                table: "SalePayments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SalePayments_IsDeleted",
                table: "SalePayments",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_SaleSessions_CreatedAt",
                table: "SaleSessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SaleSessions_CustomerId",
                table: "SaleSessions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleSessions_DeviceId",
                table: "SaleSessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleSessions_IsActive",
                table: "SaleSessions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SaleSessions_LastModified",
                table: "SaleSessions",
                column: "LastModified");

            migrationBuilder.CreateIndex(
                name: "IX_SaleSessions_SaleId",
                table: "SaleSessions",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleSessions_ShopId",
                table: "SaleSessions",
                column: "ShopId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleSessions_State",
                table: "SaleSessions",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_SaleSessions_TabName",
                table: "SaleSessions",
                column: "TabName");

            migrationBuilder.CreateIndex(
                name: "IX_SaleSessions_UserId",
                table: "SaleSessions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Suppliers_SupplierId",
                table: "Products",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Shops_ShopId",
                table: "Sales",
                column: "ShopId",
                principalTable: "Shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Suppliers_SupplierId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Shops_ShopId",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "CustomerPreferences");

            migrationBuilder.DropTable(
                name: "MembershipBenefits");

            migrationBuilder.DropTable(
                name: "SaleAuditLogs");

            migrationBuilder.DropTable(
                name: "SaleItemDiscounts");

            migrationBuilder.DropTable(
                name: "SalePayments");

            migrationBuilder.DropTable(
                name: "SaleSessions");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "CustomerMemberships");

            migrationBuilder.DropIndex(
                name: "IX_TransactionLog_Device_IsProcessed",
                table: "TransactionLogs");

            migrationBuilder.DropIndex(
                name: "IX_TransactionLog_Entity_CreatedAt",
                table: "TransactionLogs");

            migrationBuilder.DropIndex(
                name: "IX_TransactionLog_IsProcessed_CreatedAt",
                table: "TransactionLogs");

            migrationBuilder.DropIndex(
                name: "IX_Sale_Created_Amount",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sale_Customer_Created",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sale_Shop_Created_NotDeleted",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sale_Shop_Payment_Created",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sale_Status",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sale_User_Created",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_SaleItem_BatchNumber",
                table: "SaleItems");

            migrationBuilder.DropIndex(
                name: "IX_SaleDiscount_DiscountId",
                table: "SaleDiscounts");

            migrationBuilder.DropIndex(
                name: "IX_SaleDiscount_DiscountType",
                table: "SaleDiscounts");

            migrationBuilder.DropIndex(
                name: "IX_SaleDiscounts_IsDeleted",
                table: "SaleDiscounts");

            migrationBuilder.DropIndex(
                name: "IX_Product_Barcode_Shop",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Product_Expiry_Shop",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Product_Name_Shop",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Product_Shop_Active_NotDeleted",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Product_Shop_Category_Active",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Barcode",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_SupplierId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Customer_Phone",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customer_Phone_Active_NotDeleted",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customer_Spent_Tier",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customer_Tier_Active",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Business_Owner_Active_NotDeleted",
                table: "Businesses");

            migrationBuilder.DropIndex(
                name: "IX_Business_Sync_Updated",
                table: "Businesses");

            migrationBuilder.DropIndex(
                name: "IX_Business_Type_Active",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Stock");

            migrationBuilder.DropColumn(
                name: "AmountPaid",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ChangeAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "FinalTotal",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "LastModified",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TotalDiscount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TotalTax",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "IsWeightBased",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "LineDiscount",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "LineSubtotal",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "LineTax",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "LineTotal",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "ProductCode",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "ProductName",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "AuthorizedBy",
                table: "SaleDiscounts");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "SaleDiscounts");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "SaleDiscounts");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "SaleDiscounts");

            migrationBuilder.DropColumn(
                name: "DiscountName",
                table: "SaleDiscounts");

            migrationBuilder.DropColumn(
                name: "DiscountType",
                table: "SaleDiscounts");

            migrationBuilder.DropColumn(
                name: "FixedAmount",
                table: "SaleDiscounts");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "SaleDiscounts");

            migrationBuilder.DropColumn(
                name: "PercentageValue",
                table: "SaleDiscounts");

            migrationBuilder.DropColumn(
                name: "ServerSyncedAt",
                table: "SaleDiscounts");

            migrationBuilder.DropColumn(
                name: "SyncStatus",
                table: "SaleDiscounts");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "SaleDiscounts");

            migrationBuilder.DropColumn(
                name: "MaxWeightKg",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MinWeightKg",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ShopId",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Configurations");

            migrationBuilder.RenameColumn(
                name: "CalculatedAmount",
                table: "SaleDiscounts",
                newName: "DiscountAmount");

            migrationBuilder.RenameIndex(
                name: "IX_SaleDiscount_SaleId",
                table: "SaleDiscounts",
                newName: "IX_SaleDiscounts_SaleId");

            migrationBuilder.RenameIndex(
                name: "IX_SaleDiscount_AppliedAt",
                table: "SaleDiscounts",
                newName: "IX_SaleDiscounts_AppliedAt");

            migrationBuilder.AlterColumn<string>(
                name: "DiscountReason",
                table: "SaleDiscounts",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "DiscountId",
                table: "SaleDiscounts",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleDiscounts_DiscountId",
                table: "SaleDiscounts",
                column: "DiscountId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ShopId_Barcode",
                table: "Products",
                columns: new[] { "ShopId", "Barcode" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Shops_ShopId",
                table: "Sales",
                column: "ShopId",
                principalTable: "Shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
