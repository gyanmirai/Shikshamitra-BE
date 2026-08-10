using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    public partial class AddAcademicCalendarMediumScopeGenerated : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>("AcademicMediumId", "AppAcademicCalendars", "uuid", nullable: true);
            migrationBuilder.AddColumn<Guid>("AcademicMediumId", "AppAcademicCalendarSettings", "uuid", nullable: true);

            migrationBuilder.AlterColumn<Guid>("AcademicYearId", "AppAcademicCalendars", "uuid", nullable: true, oldClrType: typeof(Guid), oldType: "uuid");
            migrationBuilder.AlterColumn<Guid>("AcademicYearId", "AppAcademicCalendarSettings", "uuid", nullable: true, oldClrType: typeof(Guid), oldType: "uuid");

            migrationBuilder.DropIndex("IX_AppAcademicCalendars_AcademicYearId_IsDefault", "AppAcademicCalendars");
            migrationBuilder.DropIndex("IX_AppAcademicCalendarSettings_AcademicYearId", "AppAcademicCalendarSettings");
            migrationBuilder.CreateIndex("IX_AppAcademicCalendars_AcademicMediumId_IsDefault", "AppAcademicCalendars", new[] { "AcademicMediumId", "IsDefault" });
            migrationBuilder.CreateIndex("IX_AppAcademicCalendarSettings_AcademicMediumId", "AppAcademicCalendarSettings", "AcademicMediumId", unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex("IX_AppAcademicCalendars_AcademicMediumId_IsDefault", "AppAcademicCalendars");
            migrationBuilder.DropIndex("IX_AppAcademicCalendarSettings_AcademicMediumId", "AppAcademicCalendarSettings");
            migrationBuilder.CreateIndex("IX_AppAcademicCalendars_AcademicYearId_IsDefault", "AppAcademicCalendars", new[] { "AcademicYearId", "IsDefault" });
            migrationBuilder.CreateIndex("IX_AppAcademicCalendarSettings_AcademicYearId", "AppAcademicCalendarSettings", "AcademicYearId", unique: true);
            migrationBuilder.AlterColumn<Guid>("AcademicYearId", "AppAcademicCalendars", "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>("AcademicYearId", "AppAcademicCalendarSettings", "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
            migrationBuilder.DropColumn("AcademicMediumId", "AppAcademicCalendars");
            migrationBuilder.DropColumn("AcademicMediumId", "AppAcademicCalendarSettings");
        }
    }
}
