using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentElectivePreferenceSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "academic_StudentElectivePreferenceSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectiveGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreferredSubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreferredSubjectName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SubmittingUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academic_StudentElectivePreferenceSubmissions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ElectivePreference_ClientRequest",
                table: "academic_StudentElectivePreferenceSubmissions",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ElectivePreference_StudentYearGroup",
                table: "academic_StudentElectivePreferenceSubmissions",
                columns: new[] { "TenantId", "StudentId", "AcademicYearId", "ElectiveGroupId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "academic_StudentElectivePreferenceSubmissions");
        }
    }
}
