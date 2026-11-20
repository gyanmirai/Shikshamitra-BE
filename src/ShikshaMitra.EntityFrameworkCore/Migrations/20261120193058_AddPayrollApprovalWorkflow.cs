using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HR_PayrollApprovalSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApproverId = table.Column<Guid>(type: "uuid", nullable: true),
                    AllowSelfFinalization = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PayrollApprovalSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_PayrollCommandReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Command = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PayrollCommandReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_PayrollSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    Month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    SourceRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputRevision = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmitterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApproverId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelfFinalization = table.Column<bool>(type: "boolean", nullable: false),
                    SettingsVersion = table.Column<int>(type: "integer", nullable: false),
                    PreviousSubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    NetPay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StaffCount = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PayrollSubmissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_PayrollEmployeeClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Generation = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PayrollEmployeeClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HR_PayrollEmployeeClaims_HR_PayrollSubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "HR_PayrollSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HR_PayrollSubmissionEmployees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviewEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceDigest = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    NetPay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PayrollSubmissionEmployees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HR_PayrollSubmissionEmployees_HR_PayrollPreviewEmployees_Pr~",
                        column: x => x.PreviewEmployeeId,
                        principalTable: "HR_PayrollPreviewEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HR_PayrollSubmissionEmployees_HR_PayrollSubmissions_Submiss~",
                        column: x => x.SubmissionId,
                        principalTable: "HR_PayrollSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollApprovalSettings_TenantId",
                table: "HR_PayrollApprovalSettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollCommandReceipts_TenantId_ActorId_Command_Key",
                table: "HR_PayrollCommandReceipts",
                columns: new[] { "TenantId", "ActorId", "Command", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollEmployeeClaims_SubmissionId",
                table: "HR_PayrollEmployeeClaims",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollEmployeeClaims_TenantId_Month_EmployeeId",
                table: "HR_PayrollEmployeeClaims",
                columns: new[] { "TenantId", "Month", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollSubmissionEmployees_PreviewEmployeeId",
                table: "HR_PayrollSubmissionEmployees",
                column: "PreviewEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollSubmissionEmployees_SubmissionId_EmployeeId",
                table: "HR_PayrollSubmissionEmployees",
                columns: new[] { "SubmissionId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollSubmissions_TenantId_Month_ApproverId_SubmittedAt",
                table: "HR_PayrollSubmissions",
                columns: new[] { "TenantId", "Month", "ApproverId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollSubmissions_TenantId_Month_SubmitterId_SubmittedAt",
                table: "HR_PayrollSubmissions",
                columns: new[] { "TenantId", "Month", "SubmitterId", "SubmittedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HR_PayrollApprovalSettings");

            migrationBuilder.DropTable(
                name: "HR_PayrollCommandReceipts");

            migrationBuilder.DropTable(
                name: "HR_PayrollEmployeeClaims");

            migrationBuilder.DropTable(
                name: "HR_PayrollSubmissionEmployees");

            migrationBuilder.DropTable(
                name: "HR_PayrollSubmissions");
        }
    }
}
