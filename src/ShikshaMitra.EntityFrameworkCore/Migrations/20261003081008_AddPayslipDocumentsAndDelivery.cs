using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddPayslipDocumentsAndDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaidAt",
                table: "HR_SalarySlips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentRecordedBy",
                table: "HR_SalarySlips",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "HR_SalarySlips",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentRequestId",
                table: "HR_SalarySlips",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HR_PayslipDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Automatic = table.Column<bool>(type: "boolean", nullable: false),
                    Recipient = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ErrorCategory = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RequiredPermission = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("PK_HR_PayslipDeliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_PayslipDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SalarySlipId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthYear = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssuedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    FactsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Pdf = table.Column<byte[]>(type: "bytea", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReplacementReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReplacesDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_PayslipDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_PayslipSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SchoolName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    SchoolAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AutomaticEmail = table.Column<bool>(type: "boolean", nullable: false),
                    LastRequestId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_HR_PayslipSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayslipDeliveries_DocumentId_RequestId",
                table: "HR_PayslipDeliveries",
                columns: new[] { "DocumentId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayslipDeliveries_TenantId_DocumentId_CreationTime",
                table: "HR_PayslipDeliveries",
                columns: new[] { "TenantId", "DocumentId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayslipDocuments_SalarySlipId_RequestId",
                table: "HR_PayslipDocuments",
                columns: new[] { "SalarySlipId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayslipDocuments_SalarySlipId_Revision",
                table: "HR_PayslipDocuments",
                columns: new[] { "SalarySlipId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayslipDocuments_TenantId_EmployeeId_MonthYear",
                table: "HR_PayslipDocuments",
                columns: new[] { "TenantId", "EmployeeId", "MonthYear" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayslipSettings_TenantId",
                table: "HR_PayslipSettings",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HR_PayslipDeliveries");

            migrationBuilder.DropTable(
                name: "HR_PayslipDocuments");

            migrationBuilder.DropTable(
                name: "HR_PayslipSettings");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "PaymentRecordedBy",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "PaymentRequestId",
                table: "HR_SalarySlips");
        }
    }
}
