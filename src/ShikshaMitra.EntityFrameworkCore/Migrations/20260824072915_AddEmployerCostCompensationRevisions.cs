using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployerCostCompensationRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultProtectionPolicy",
                table: "HR_SalaryStructureTemplateVersions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefaultSalaryBasis",
                table: "HR_SalaryStructureTemplateVersions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CompensationNormalizedProtectedAmount",
                table: "HR_SalarySlips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CompensationProtectedAmount",
                table: "HR_SalarySlips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompensationProtectionPolicy",
                table: "HR_SalarySlips",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CompensationQuotedAmount",
                table: "HR_SalarySlips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompensationRevisionId",
                table: "HR_SalarySlips",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompensationRevisionNumber",
                table: "HR_SalarySlips",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompensationSalaryBasis",
                table: "HR_SalarySlips",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CompensationTargetDifference",
                table: "HR_SalarySlips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EmployeeStateInsuranceGovernmentContribution",
                table: "HR_SalarySlips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "EmployerContributionTotal",
                table: "HR_PayrollRunItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<decimal>(
                name: "CompensationNormalizedProtectedAmount",
                table: "HR_PayrollRunItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CompensationProtectedAmount",
                table: "HR_PayrollRunItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompensationProtectionPolicy",
                table: "HR_PayrollRunItems",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CompensationQuotedAmount",
                table: "HR_PayrollRunItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompensationRevisionId",
                table: "HR_PayrollRunItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompensationRevisionNumber",
                table: "HR_PayrollRunItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompensationSalaryBasis",
                table: "HR_PayrollRunItems",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CompensationTargetDifference",
                table: "HR_PayrollRunItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EmployeeStateInsuranceGovernmentContribution",
                table: "HR_PayrollRunItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EmployerCost",
                table: "HR_PayrollRunItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DisabilityEvidenceReference",
                table: "AppEmployeeStatutoryEnrollments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmployerShareReliefEligible",
                table: "AppEmployeeStatutoryEnrollments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmployerShareReliefFrom",
                table: "AppEmployeeStatutoryEnrollments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmployerShareReliefTo",
                table: "AppEmployeeStatutoryEnrollments",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HR_EmployeeCompensationRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    EffectiveMonth = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SalaryBasis = table.Column<int>(type: "integer", nullable: false),
                    ProtectionPolicy = table.Column<int>(type: "integer", nullable: false),
                    QuotedMonthlyAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProtectedMonthlyAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EmployerCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SourceSalaryTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSalaryTemplateVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSalaryTemplateVersion = table.Column<int>(type: "integer", nullable: true),
                    PfRuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    StatutoryProfile = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CalculationSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_HR_EmployeeCompensationRevisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_EmployeeCompensationRevisionLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComponentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsEarning = table.Column<bool>(type: "boolean", nullable: false),
                    ComponentType = table.Column<int>(type: "integer", nullable: false),
                    DependOn = table.Column<int>(type: "integer", nullable: false),
                    SemanticType = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_EmployeeCompensationRevisionLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HR_EmployeeCompensationRevisionLines_HR_EmployeeCompensatio~",
                        column: x => x.RevisionId,
                        principalTable: "HR_EmployeeCompensationRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HR_EmployeeCompensationRevisionLines_RevisionId",
                table: "HR_EmployeeCompensationRevisionLines",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_EmployeeCompensationRevisionLines_TenantId_EmployeeId",
                table: "HR_EmployeeCompensationRevisionLines",
                columns: new[] { "TenantId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_EmployeeCompensationRevisionLines_TenantId_RevisionId_Di~",
                table: "HR_EmployeeCompensationRevisionLines",
                columns: new[] { "TenantId", "RevisionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_EmployeeCompensationRevisions_TenantId_EmployeeId_Effect~",
                table: "HR_EmployeeCompensationRevisions",
                columns: new[] { "TenantId", "EmployeeId", "EffectiveMonth", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_EmployeeCompensationRevisions_TenantId_EmployeeId_Revisi~",
                table: "HR_EmployeeCompensationRevisions",
                columns: new[] { "TenantId", "EmployeeId", "RevisionNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HR_EmployeeCompensationRevisionLines");

            migrationBuilder.DropTable(
                name: "HR_EmployeeCompensationRevisions");

            migrationBuilder.DropColumn(
                name: "DefaultProtectionPolicy",
                table: "HR_SalaryStructureTemplateVersions");

            migrationBuilder.DropColumn(
                name: "DefaultSalaryBasis",
                table: "HR_SalaryStructureTemplateVersions");

            migrationBuilder.DropColumn(
                name: "CompensationNormalizedProtectedAmount",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "CompensationProtectedAmount",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "CompensationProtectionPolicy",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "CompensationQuotedAmount",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "CompensationRevisionId",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "CompensationRevisionNumber",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "CompensationSalaryBasis",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "CompensationTargetDifference",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "EmployeeStateInsuranceGovernmentContribution",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "CompensationNormalizedProtectedAmount",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "CompensationProtectedAmount",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "CompensationProtectionPolicy",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "CompensationQuotedAmount",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "CompensationRevisionId",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "CompensationRevisionNumber",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "CompensationSalaryBasis",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "CompensationTargetDifference",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "EmployeeStateInsuranceGovernmentContribution",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "EmployerCost",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "DisabilityEvidenceReference",
                table: "AppEmployeeStatutoryEnrollments");

            migrationBuilder.DropColumn(
                name: "EmployerShareReliefEligible",
                table: "AppEmployeeStatutoryEnrollments");

            migrationBuilder.DropColumn(
                name: "EmployerShareReliefFrom",
                table: "AppEmployeeStatutoryEnrollments");

            migrationBuilder.DropColumn(
                name: "EmployerShareReliefTo",
                table: "AppEmployeeStatutoryEnrollments");

            migrationBuilder.AlterColumn<decimal>(
                name: "EmployerContributionTotal",
                table: "HR_PayrollRunItems",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);
        }
    }
}
