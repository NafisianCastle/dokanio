using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shared.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerMembershipAndEnhancedIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create CustomerMembership table
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

            // Create MembershipBenefit table
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

            // Create CustomerPreference table
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

            // Create indexes for CustomerMembership
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
                name: "IX_CustomerMembership_Customer_Active_NotDeleted",
                table: "CustomerMemberships",
                columns: new[] { "CustomerId", "IsActive", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMembership_Tier_Active",
                table: "CustomerMemberships",
                columns: new[] { "Tier", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMembership_Expiry_Active",
                table: "CustomerMemberships",
                columns: new[] { "ExpiryDate", "IsActive" },
                filter: "ExpiryDate IS NOT NULL");

            // Create indexes for MembershipBenefit
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
                name: "IX_MembershipBenefit_Membership_Active_NotDeleted",
                table: "MembershipBenefits",
                columns: new[] { "CustomerMembershipId", "IsActive", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipBenefit_Type_Active",
                table: "MembershipBenefits",
                columns: new[] { "Type", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipBenefit_DateRange_Active",
                table: "MembershipBenefits",
                columns: new[] { "StartDate", "EndDate", "IsActive" });

            // Create indexes for CustomerPreference
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
                name: "IX_CustomerPreference_Customer_Key",
                table: "CustomerPreferences",
                columns: new[] { "CustomerId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPreference_Customer_Category_Active",
                table: "CustomerPreferences",
                columns: new[] { "CustomerId", "Category", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerPreferences");

            migrationBuilder.DropTable(
                name: "MembershipBenefits");

            migrationBuilder.DropTable(
                name: "CustomerMemberships");
        }
    }
}