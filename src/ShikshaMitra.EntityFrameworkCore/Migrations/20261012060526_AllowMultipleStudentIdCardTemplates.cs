using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations;

public partial class AllowMultipleStudentIdCardTemplates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AppStudentIdCardTemplates_TenantId",
            table: "AppStudentIdCardTemplates");

        migrationBuilder.CreateIndex(
            name: "IX_AppStudentIdCardTemplates_TenantId",
            table: "AppStudentIdCardTemplates",
            column: "TenantId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AppStudentIdCardTemplates_TenantId",
            table: "AppStudentIdCardTemplates");

        migrationBuilder.CreateIndex(
            name: "IX_AppStudentIdCardTemplates_TenantId",
            table: "AppStudentIdCardTemplates",
            column: "TenantId",
            unique: true);
    }
}
