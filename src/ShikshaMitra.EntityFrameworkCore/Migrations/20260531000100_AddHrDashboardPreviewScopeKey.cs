using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations
{
    [DbContext(typeof(ShikshaMitraDbContext))]
    [Migration("20260531000100_AddHrDashboardPreviewScopeKey")]
    public partial class AddHrDashboardPreviewScopeKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HR_DashboardPreviews_Tenant_Key",
                table: "HR_DashboardPreviews");

            migrationBuilder.AddColumn<string>(
                name: "ScopeKey",
                table: "HR_DashboardPreviews",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "hr-wide");

            migrationBuilder.CreateIndex(
                name: "IX_HR_DashboardPreviews_Tenant_Key_Scope",
                table: "HR_DashboardPreviews",
                columns: new[] { "TenantId", "DashboardKey", "ScopeKey" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HR_DashboardPreviews_Tenant_Key_Scope",
                table: "HR_DashboardPreviews");

            migrationBuilder.DropColumn(
                name: "ScopeKey",
                table: "HR_DashboardPreviews");

            migrationBuilder.CreateIndex(
                name: "IX_HR_DashboardPreviews_Tenant_Key",
                table: "HR_DashboardPreviews",
                columns: new[] { "TenantId", "DashboardKey" },
                unique: true);
        }
    }
}
