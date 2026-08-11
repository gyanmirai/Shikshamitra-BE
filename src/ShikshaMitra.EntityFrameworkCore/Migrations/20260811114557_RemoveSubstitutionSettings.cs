using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations;

public partial class RemoveSubstitutionSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AppAcademicSubstitutionSettings");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AppAcademicSubstitutionSettings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                MaxSubstitutionsPerTeacherPerDay = table.Column<int>(type: "integer", nullable: false),
                MaxSubstitutionsPerTeacherPerWeek = table.Column<int>(type: "integer", nullable: false),
                AcknowledgementRequiredByDefault = table.Column<bool>(type: "boolean", nullable: false),
                AutoReminderEnabled = table.Column<bool>(type: "boolean", nullable: false),
                ReminderBeforeMinutes = table.Column<int>(type: "integer", nullable: false),
                NotifyLeadershipForUrgent = table.Column<bool>(type: "boolean", nullable: false),
                AllowTeacherReject = table.Column<bool>(type: "boolean", nullable: false),
                StudentVisibleDailySheet = table.Column<bool>(type: "boolean", nullable: false),
                ExtraProperties = table.Column<string>(type: "text", nullable: false),
                ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppAcademicSubstitutionSettings", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AppAcademicSubstitutionSettings_TenantId_AcademicYearId",
            table: "AppAcademicSubstitutionSettings",
            columns: new[] { "TenantId", "AcademicYearId" });
    }
}
