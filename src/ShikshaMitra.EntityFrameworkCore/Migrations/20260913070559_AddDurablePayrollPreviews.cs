using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddDurablePayrollPreviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PreviewRunId",
                table: "HR_PayrollRunReviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HR_PayrollInputRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PayrollInputRevisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_PayrollPreviewRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScopeFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    InputRevision = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SummaryJson = table.Column<string>(type: "text", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    EmployeeCount = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CompletedEmployees = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PayrollPreviewRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_PayrollPreviewEmployees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EmployeeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ItemJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PayrollPreviewEmployees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HR_PayrollPreviewEmployees_HR_PayrollPreviewRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "HR_PayrollPreviewRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollPreviewEmployees_RunId_EmployeeId",
                table: "HR_PayrollPreviewEmployees",
                columns: new[] { "RunId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollPreviewEmployees_RunId_EmployeeName_EmployeeId",
                table: "HR_PayrollPreviewEmployees",
                columns: new[] { "RunId", "EmployeeName", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollPreviewRuns_TenantId_Month_AcademicYearId_ScopeFi~",
                table: "HR_PayrollPreviewRuns",
                columns: new[] { "TenantId", "Month", "AcademicYearId", "ScopeFingerprint", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HR_PayrollInputRevisions");

            migrationBuilder.DropTable(
                name: "HR_PayrollPreviewEmployees");

            migrationBuilder.DropTable(
                name: "HR_PayrollPreviewRuns");

            migrationBuilder.DropColumn(
                name: "PreviewRunId",
                table: "HR_PayrollRunReviews");
        }
    }
}
