using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JBDS.GymApp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberPortalWorkoutsAndDoorAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DoorAccessEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                    MemberId = table.Column<Guid>(type: "char(36)", nullable: false),
                    DoorName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    TriggeredAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Granted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    UnlockDurationSeconds = table.Column<int>(type: "int", nullable: false),
                    IsSimulatedHardware = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Reason = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    ProviderResponse = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoorAccessEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoorAccessEvents_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "WorkoutLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                    MemberId = table.Column<Guid>(type: "char(36)", nullable: false),
                    WorkoutDateUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Title = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Category = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    EstimatedCalories = table.Column<int>(type: "int", nullable: true),
                    ExercisesSummary = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkoutLogs_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DoorAccessEvents_MemberId",
                table: "DoorAccessEvents",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_DoorAccessEvents_TenantId_ProductId_TriggeredAtUtc",
                table: "DoorAccessEvents",
                columns: new[] { "TenantId", "ProductId", "TriggeredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutLogs_MemberId",
                table: "WorkoutLogs",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutLogs_TenantId_ProductId_MemberId_WorkoutDateUtc",
                table: "WorkoutLogs",
                columns: new[] { "TenantId", "ProductId", "MemberId", "WorkoutDateUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DoorAccessEvents");

            migrationBuilder.DropTable(
                name: "WorkoutLogs");
        }
    }
}
