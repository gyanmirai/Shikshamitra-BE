using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionCrmTimelineUtcSort : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionCrmTimelineEvents_TenantId_LeadId_OccurredAt",
                table: "AppAdmissionCrmTimelineEvents");

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurredAtUtc",
                table: "AppAdmissionCrmTimelineEvents",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.Sql(
                """
                UPDATE "AppAdmissionCrmTimelineEvents"
                SET "OccurredAtUtc" = "OccurredAt";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmTimelineEvents_TenantId_LeadId_OccurredAtUtc",
                table: "AppAdmissionCrmTimelineEvents",
                columns: new[] { "TenantId", "LeadId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionCrmTimelineEvents_TenantId_LeadId_OccurredAtUtc",
                table: "AppAdmissionCrmTimelineEvents");

            migrationBuilder.DropColumn(
                name: "OccurredAtUtc",
                table: "AppAdmissionCrmTimelineEvents");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmTimelineEvents_TenantId_LeadId_OccurredAt",
                table: "AppAdmissionCrmTimelineEvents",
                columns: new[] { "TenantId", "LeadId", "OccurredAt" });
        }
    }
}
