using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollAttendanceImportLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HR_AttendanceImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ImportedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportedByName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PayrollMonthYear = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    PayrollMonth = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    ValidRows = table.Column<int>(type: "integer", nullable: false),
                    InvalidRows = table.Column<int>(type: "integer", nullable: false),
                    CreateCount = table.Column<int>(type: "integer", nullable: false),
                    ReplaceCount = table.Column<int>(type: "integer", nullable: false),
                    ProcessedRows = table.Column<int>(type: "integer", nullable: false),
                    CreatedCount = table.Column<int>(type: "integer", nullable: false),
                    ReplacedCount = table.Column<int>(type: "integer", nullable: false),
                    PreviewToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PreviewTokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorsSummary = table.Column<string>(type: "jsonb", nullable: false),
                    WarningsSummary = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("PK_HR_AttendanceImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_AttendanceImportRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AttendanceDate = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EmployeeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EmployeeName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ContactNo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TimeIn = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TimeOut = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Errors = table.Column<string>(type: "jsonb", nullable: false),
                    Warnings = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("PK_HR_AttendanceImportRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HR_AttendanceImportRows_HR_AttendanceImportBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "HR_AttendanceImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendanceImportBatches_ImportedAt",
                table: "HR_AttendanceImportBatches",
                column: "ImportedAt");

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendanceImportBatches_PayrollMonthYear",
                table: "HR_AttendanceImportBatches",
                column: "PayrollMonthYear");

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendanceImportBatches_Status",
                table: "HR_AttendanceImportBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendanceImportBatches_TenantId",
                table: "HR_AttendanceImportBatches",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendanceImportRows_BatchId",
                table: "HR_AttendanceImportRows",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendanceImportRows_BatchId_RowNumber",
                table: "HR_AttendanceImportRows",
                columns: new[] { "BatchId", "RowNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_AttendanceImportRows_TenantId",
                table: "HR_AttendanceImportRows",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HR_AttendanceImportRows");

            migrationBuilder.DropTable(
                name: "HR_AttendanceImportBatches");
        }
    }
}
