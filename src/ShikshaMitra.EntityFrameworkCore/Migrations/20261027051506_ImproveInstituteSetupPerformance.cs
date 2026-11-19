using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class ImproveInstituteSetupPerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "AppInstituteReportJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpiresAt",
                table: "AppInstituteReportJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseId",
                table: "AppInstituteReportJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportTypeId",
                table: "AppInstituteReportJobs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "AppInstituteReportJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                table: "academic_Divisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "AppInstituteReportJobs"
                SET "ReportTypeId" = COALESCE(NULLIF("RequestJson"::jsonb ->> 'ReportType', ''),
                    NULLIF("RequestJson"::jsonb ->> 'reportType', ''), '');

                UPDATE "academic_Divisions" d SET "RoomId" = r."Id"
                FROM "academic_Rooms" r
                WHERE d."TenantId" = r."TenantId"
                    AND lower(btrim(d."RoomNumber")) = lower(r."Id"::text);

                WITH matches AS (
                    SELECT d."Id" AS division_id, r."Id" AS room_id,
                        count(*) OVER (PARTITION BY d."Id") AS candidates
                    FROM "academic_Divisions" d
                    JOIN "academic_Rooms" r ON d."TenantId" = r."TenantId"
                        AND lower(btrim(d."RoomNumber")) = lower(btrim(r."RoomName"))
                    WHERE d."RoomId" IS NULL AND btrim(d."RoomNumber") <> ''
                        AND btrim(d."RoomNumber") !~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
                )
                UPDATE "academic_Divisions" d SET "RoomId" = m.room_id
                FROM matches m WHERE d."Id" = m.division_id AND m.candidates = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AppInstituteReportJobs_TenantId_Status_LeaseExpiresAt",
                table: "AppInstituteReportJobs",
                columns: new[] { "TenantId", "Status", "LeaseExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_academic_Divisions_RoomId",
                table: "academic_Divisions",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_academic_Divisions_TenantId_RoomId",
                table: "academic_Divisions",
                columns: new[] { "TenantId", "RoomId" });

            migrationBuilder.AddForeignKey(
                name: "FK_academic_Divisions_academic_Rooms_RoomId",
                table: "academic_Divisions",
                column: "RoomId",
                principalTable: "academic_Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_academic_Divisions_academic_Rooms_RoomId",
                table: "academic_Divisions");

            migrationBuilder.DropIndex(
                name: "IX_AppInstituteReportJobs_TenantId_Status_LeaseExpiresAt",
                table: "AppInstituteReportJobs");

            migrationBuilder.DropIndex(
                name: "IX_academic_Divisions_RoomId",
                table: "academic_Divisions");

            migrationBuilder.DropIndex(
                name: "IX_academic_Divisions_TenantId_RoomId",
                table: "academic_Divisions");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "AppInstituteReportJobs");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                table: "AppInstituteReportJobs");

            migrationBuilder.DropColumn(
                name: "LeaseId",
                table: "AppInstituteReportJobs");

            migrationBuilder.DropColumn(
                name: "ReportTypeId",
                table: "AppInstituteReportJobs");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "AppInstituteReportJobs");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "academic_Divisions");
        }
    }
}
