using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class LeaveYearEndSettlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSystemGenerated",
                table: "HR_PayrollAdjustments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceId",
                table: "HR_PayrollAdjustments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "HR_PayrollAdjustments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HR_LeaveYearEndSettlementItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceAcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetAcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceLeavePolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceLeavePolicyRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetLeavePolicyRuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccruedDays = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CarryInDays = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ApprovedUsedDays = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PendingDays = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnusedDays = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CarriedDays = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EncashedDays = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ForfeitedDays = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PayrollMonth = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IssueCodesJson = table.Column<string>(type: "jsonb", nullable: false),
                    PayrollAdjustmentId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_HR_LeaveYearEndSettlementItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_LeaveYearEndSettlementRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetAcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PreviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinalizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalItems = table.Column<int>(type: "integer", nullable: false),
                    FinalizedItems = table.Column<int>(type: "integer", nullable: false),
                    BlockedItems = table.Column<int>(type: "integer", nullable: false),
                    TotalCarriedDays = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalEncashedDays = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalForfeitedDays = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalEncashmentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_HR_LeaveYearEndSettlementRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollAdjustments_Tenant_Source",
                table: "HR_PayrollAdjustments",
                columns: new[] { "TenantId", "SourceType", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_LeaveYearEndItems_CarryIn",
                table: "HR_LeaveYearEndSettlementItems",
                columns: new[] { "TenantId", "TargetAcademicYearId", "EmployeeId", "TargetLeavePolicyRuleId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_LeaveYearEndItems_Payroll",
                table: "HR_LeaveYearEndSettlementItems",
                columns: new[] { "TenantId", "PayrollMonth", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_LeaveYearEndItems_Run_Employee_SourceRule",
                table: "HR_LeaveYearEndSettlementItems",
                columns: new[] { "TenantId", "RunId", "EmployeeId", "SourceLeavePolicyRuleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_LeaveYearEndRuns_Tenant_SourceYear",
                table: "HR_LeaveYearEndSettlementRuns",
                columns: new[] { "TenantId", "AcademicYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_LeaveYearEndRuns_Tenant_Status",
                table: "HR_LeaveYearEndSettlementRuns",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HR_LeaveYearEndSettlementItems");

            migrationBuilder.DropTable(
                name: "HR_LeaveYearEndSettlementRuns");

            migrationBuilder.DropIndex(
                name: "IX_HR_PayrollAdjustments_Tenant_Source",
                table: "HR_PayrollAdjustments");

            migrationBuilder.DropColumn(
                name: "IsSystemGenerated",
                table: "HR_PayrollAdjustments");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "HR_PayrollAdjustments");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "HR_PayrollAdjustments");
        }
    }
}
