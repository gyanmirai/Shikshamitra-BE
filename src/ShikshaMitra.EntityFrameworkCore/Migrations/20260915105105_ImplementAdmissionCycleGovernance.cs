using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class ImplementAdmissionCycleGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "AppAdmissionCycles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedBy",
                table: "AppAdmissionCycles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "AppAdmissionCycles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosedBy",
                table: "AppAdmissionCycles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "AppAdmissionCycles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReopenedAt",
                table: "AppAdmissionCycles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReopenedBy",
                table: "AppAdmissionCycles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledCloseAt",
                table: "AppAdmissionCycles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledPublishAt",
                table: "AppAdmissionCycles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppAdmissionCycleHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdmissionCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: true),
                    ToStatus = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAdmissionCycleHistories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCycleHistories_TenantId_AdmissionCycleId_Occurr~",
                table: "AppAdmissionCycleHistories",
                columns: new[] { "TenantId", "AdmissionCycleId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAdmissionCycleHistories");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "AppAdmissionCycles");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "AppAdmissionCycles");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "AppAdmissionCycles");

            migrationBuilder.DropColumn(
                name: "ClosedBy",
                table: "AppAdmissionCycles");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "AppAdmissionCycles");

            migrationBuilder.DropColumn(
                name: "ReopenedAt",
                table: "AppAdmissionCycles");

            migrationBuilder.DropColumn(
                name: "ReopenedBy",
                table: "AppAdmissionCycles");

            migrationBuilder.DropColumn(
                name: "ScheduledCloseAt",
                table: "AppAdmissionCycles");

            migrationBuilder.DropColumn(
                name: "ScheduledPublishAt",
                table: "AppAdmissionCycles");
        }
    }
}
