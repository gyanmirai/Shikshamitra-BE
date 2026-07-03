using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarAuditLogRichFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorEmailSnapshot",
                table: "AppAcademicCalendarAuditLogs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorNameSnapshot",
                table: "AppAcademicCalendarAuditLogs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorRoleSnapshot",
                table: "AppAcademicCalendarAuditLogs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Browser",
                table: "AppAcademicCalendarAuditLogs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangedFieldsJson",
                table: "AppAcademicCalendarAuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "AppAcademicCalendarAuditLogs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Device",
                table: "AppAcademicCalendarAuditLogs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityName",
                table: "AppAcademicCalendarAuditLogs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityType",
                table: "AppAcademicCalendarAuditLogs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasDiff",
                table: "AppAcademicCalendarAuditLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "HttpMethod",
                table: "AppAcademicCalendarAuditLogs",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "AppAcademicCalendarAuditLogs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Module",
                table: "AppAcademicCalendarAuditLogs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "CalendarManagement");

            migrationBuilder.AddColumn<string>(
                name: "OperatingSystem",
                table: "AppAcademicCalendarAuditLogs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestPath",
                table: "AppAcademicCalendarAuditLogs",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "AppAcademicCalendarAuditLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubModule",
                table: "AppAcademicCalendarAuditLogs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicCalendarAuditLogs_Tenant_Module_ChangedAt",
                table: "AppAcademicCalendarAuditLogs",
                columns: new[] { "TenantId", "Module", "ChangedAt" });

            // Backfill: existing rows all default to Module = 'CalendarManagement' above, but
            // pre-existing Emergency Closure audit rows (Action = 8) should be tagged separately
            // so they stay excluded from the Calendar Management audit log screen, matching how
            // new Emergency Closure writes are tagged going forward.
            migrationBuilder.Sql(
                "UPDATE \"AppAcademicCalendarAuditLogs\" SET \"Module\" = 'EmergencyClosure' WHERE \"Action\" = 8;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppAcademicCalendarAuditLogs_Tenant_Module_ChangedAt",
                table: "AppAcademicCalendarAuditLogs");

            migrationBuilder.DropColumn(
                name: "ActorEmailSnapshot",
                table: "AppAcademicCalendarAuditLogs");

            migrationBuilder.DropColumn(
                name: "ActorNameSnapshot",
                table: "AppAcademicCalendarAuditLogs");

            migrationBuilder.DropColumn(
                name: "ActorRoleSnapshot",
                table: "AppAcademicCalendarAuditLogs");

            migrationBuilder.DropColumn(
                name: "Browser",
                table: "AppAcademicCalendarAuditLogs");

            migrationBuilder.DropColumn(
                name: "ChangedFieldsJson",
                table: "AppAcademicCalendarAuditLogs");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "AppAcademicCalendarAuditLogs");

            migrationBuilder.DropColumn(
                name: "Device",
                table: "AppAcademicCalendarAuditLogs");

            migrationBuilder.DropColumn(
                name: "EntityName",
                table: "AppAcademicCalendarAuditLogs");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "AppAcademicCalendarAuditLogs");

            migrationBuilder.DropColumn(
                name: "HasDiff",
                table: "AppAcademicCalendarAuditLogs");

            migrationBuilder.DropColumn(
                name: "HttpMethod",
                table: "AppAcademicCalendarAuditLogs");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "AppAcademicCalendarAuditLogs");

            migrationBuilder.DropColumn(
                name: "Module",
                table: "AppAcademicCalendarAuditLogs");

            migrationBuilder.DropColumn(
                name: "OperatingSystem",
                table: "AppAcademicCalendarAuditLogs");

            migrationBuilder.DropColumn(
                name: "RequestPath",
                table: "AppAcademicCalendarAuditLogs");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "AppAcademicCalendarAuditLogs");

            migrationBuilder.DropColumn(
                name: "SubModule",
                table: "AppAcademicCalendarAuditLogs");
        }
    }
}
