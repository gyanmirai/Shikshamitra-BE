using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class HrAttendanceDayLockTenantDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "AppStaffAttendanceDayLocks" AS t
                USING (
                    SELECT "Id"
                    FROM (
                        SELECT
                            "Id",
                            ROW_NUMBER() OVER (
                                PARTITION BY "TenantId", "LockDate"
                                ORDER BY "LockedAt" DESC, "CreationTime" DESC, "Id"
                            ) AS rn
                        FROM "AppStaffAttendanceDayLocks"
                    ) ranked
                    WHERE ranked.rn > 1
                ) d
                WHERE t."Id" = d."Id";
                """);

            migrationBuilder.DropIndex(
                name: "IX_StaffAttendanceDayLocks_EducationMediumStageId",
                table: "AppStaffAttendanceDayLocks");

            migrationBuilder.DropIndex(
                name: "IX_StaffAttendanceDayLocks_Tenant_Stage_Date",
                table: "AppStaffAttendanceDayLocks");

            migrationBuilder.DropColumn(
                name: "EducationMediumStageId",
                table: "AppStaffAttendanceDayLocks");

            migrationBuilder.CreateIndex(
                name: "IX_StaffAttendanceDayLocks_Tenant_Date",
                table: "AppStaffAttendanceDayLocks",
                columns: new[] { "TenantId", "LockDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffAttendanceDayLocks_Tenant_Date",
                table: "AppStaffAttendanceDayLocks");

            migrationBuilder.AddColumn<Guid>(
                name: "EducationMediumStageId",
                table: "AppStaffAttendanceDayLocks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_StaffAttendanceDayLocks_EducationMediumStageId",
                table: "AppStaffAttendanceDayLocks",
                column: "EducationMediumStageId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffAttendanceDayLocks_Tenant_Stage_Date",
                table: "AppStaffAttendanceDayLocks",
                columns: new[] { "TenantId", "EducationMediumStageId", "LockDate" },
                unique: true);
        }
    }
}
