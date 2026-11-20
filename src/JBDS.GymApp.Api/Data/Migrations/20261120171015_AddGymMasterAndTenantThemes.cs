using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JBDS.GymApp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGymMasterAndTenantThemes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeroTagline",
                table: "Tenants",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThemePreset",
                table: "Tenants",
                type: "longtext",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "DoorPinCode",
                table: "Members",
                type: "longtext",
                nullable: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "WaiverSignedAtUtc",
                table: "Members",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaiverSignedName",
                table: "Members",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaiverVersion",
                table: "Members",
                type: "longtext",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookableResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    ResourceType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "longtext", nullable: true),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookableResources", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClubAnnouncements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Title = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false),
                    Body = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false),
                    Category = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    IsPinned = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByName = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubAnnouncements", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PosProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Sku = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Category = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    StockQuantity = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosProducts", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PosSales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                    MemberId = table.Column<Guid>(type: "char(36)", nullable: true),
                    BuyerName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    SoldAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SoldByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ItemsSummary = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosSales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PosSales_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ProspectLeads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                    FullName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    Source = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Stage = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ConvertedMemberId = table.Column<Guid>(type: "char(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProspectLeads", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ResourceBookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                    BookableResourceId = table.Column<Guid>(type: "char(36)", nullable: false),
                    MemberId = table.Column<Guid>(type: "char(36)", nullable: false),
                    StartTimeUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EndTimeUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceBookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceBookings_BookableResources_BookableResourceId",
                        column: x => x.BookableResourceId,
                        principalTable: "BookableResources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResourceBookings_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_BookableResources_TenantId_ProductId_IsActive",
                table: "BookableResources",
                columns: new[] { "TenantId", "ProductId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubAnnouncements_TenantId_ProductId_PublishedAtUtc",
                table: "ClubAnnouncements",
                columns: new[] { "TenantId", "ProductId", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PosProducts_TenantId_ProductId_Sku",
                table: "PosProducts",
                columns: new[] { "TenantId", "ProductId", "Sku" });

            migrationBuilder.CreateIndex(
                name: "IX_PosSales_MemberId",
                table: "PosSales",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_PosSales_TenantId_ProductId_SoldAtUtc",
                table: "PosSales",
                columns: new[] { "TenantId", "ProductId", "SoldAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProspectLeads_TenantId_ProductId_Stage",
                table: "ProspectLeads",
                columns: new[] { "TenantId", "ProductId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceBookings_BookableResourceId",
                table: "ResourceBookings",
                column: "BookableResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceBookings_MemberId",
                table: "ResourceBookings",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceBookings_TenantId_ProductId_StartTimeUtc",
                table: "ResourceBookings",
                columns: new[] { "TenantId", "ProductId", "StartTimeUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClubAnnouncements");

            migrationBuilder.DropTable(
                name: "PosProducts");

            migrationBuilder.DropTable(
                name: "PosSales");

            migrationBuilder.DropTable(
                name: "ProspectLeads");

            migrationBuilder.DropTable(
                name: "ResourceBookings");

            migrationBuilder.DropTable(
                name: "BookableResources");

            migrationBuilder.DropColumn(
                name: "HeroTagline",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ThemePreset",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DoorPinCode",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "WaiverSignedAtUtc",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "WaiverSignedName",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "WaiverVersion",
                table: "Members");
        }
    }
}
