using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    [Migration("20260605120000_add_performance_sqaaf_metadata")]
    public partial class add_performance_sqaaf_metadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppPerformanceTemplates_TenantId_StaffCategory_Status",
                table: "AppPerformanceTemplates");

            migrationBuilder.AddColumn<int>(
                name: "BoardType",
                table: "AppPerformanceTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GujaratiName",
                table: "AppPerformanceTemplates",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GujaratiName",
                table: "AppPerformanceTemplateSections",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SqaaDomain",
                table: "AppPerformanceTemplateSections",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<string>(
                name: "GujaratiName",
                table: "AppPerformanceTemplateCriteria",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SqaaDomain",
                table: "AppPerformanceTemplateCriteria",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.CreateIndex(
                name: "IX_AppPerformanceTemplates_TenantId_BoardType_StaffCategory~",
                table: "AppPerformanceTemplates",
                columns: new[] { "TenantId", "BoardType", "StaffCategory", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppPerformanceTemplates_TenantId_BoardType_StaffCategory~",
                table: "AppPerformanceTemplates");

            migrationBuilder.DropColumn(
                name: "BoardType",
                table: "AppPerformanceTemplates");

            migrationBuilder.DropColumn(
                name: "GujaratiName",
                table: "AppPerformanceTemplates");

            migrationBuilder.DropColumn(
                name: "GujaratiName",
                table: "AppPerformanceTemplateSections");

            migrationBuilder.DropColumn(
                name: "SqaaDomain",
                table: "AppPerformanceTemplateSections");

            migrationBuilder.DropColumn(
                name: "GujaratiName",
                table: "AppPerformanceTemplateCriteria");

            migrationBuilder.DropColumn(
                name: "SqaaDomain",
                table: "AppPerformanceTemplateCriteria");

            migrationBuilder.CreateIndex(
                name: "IX_AppPerformanceTemplates_TenantId_StaffCategory_Status",
                table: "AppPerformanceTemplates",
                columns: new[] { "TenantId", "StaffCategory", "Status" });
        }
    }
}
