using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddPayslipCorrectionDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HR_PayslipCorrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SalarySlipId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceMonth = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    AttendanceJson = table.Column<string>(type: "jsonb", nullable: false),
                    CandidateJson = table.Column<string>(type: "jsonb", nullable: true),
                    InputRevision = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResultDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SettlementMonth = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PayslipCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HR_PayslipCorrections_HR_PayslipDocuments_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalTable: "HR_PayslipDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayslipCorrections_SalarySlipId",
                table: "HR_PayslipCorrections",
                column: "SalarySlipId",
                unique: true,
                filter: "\"Status\" IN ('DRAFT', 'CALCULATED', 'REVIEWED') AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayslipCorrections_SalarySlipId_RequestId",
                table: "HR_PayslipCorrections",
                columns: new[] { "SalarySlipId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayslipCorrections_SourceDocumentId",
                table: "HR_PayslipCorrections",
                column: "SourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayslipCorrections_TenantId_EmployeeId_SourceMonth",
                table: "HR_PayslipCorrections",
                columns: new[] { "TenantId", "EmployeeId", "SourceMonth" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HR_PayslipCorrections");
        }
    }
}
