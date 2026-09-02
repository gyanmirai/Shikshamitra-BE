using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddExaminationSeatingPlanSessionKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SessionKey",
                table: "AppExamSeatingPlans",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSeatingPlans_TenantId_ExamId_SessionKey",
                table: "AppExamSeatingPlans",
                columns: new[] { "TenantId", "ExamId", "SessionKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppExamSeatingPlans_TenantId_ExamId_SessionKey",
                table: "AppExamSeatingPlans");

            migrationBuilder.DropColumn(
                name: "SessionKey",
                table: "AppExamSeatingPlans");
        }
    }
}
