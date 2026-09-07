using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddEsiStatutoryPayrollEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EmployeeStateInsuranceContributionPeriodEnd",
                table: "HR_SalarySlips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmployeeStateInsuranceContributionPeriodStart",
                table: "HR_SalarySlips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeStateInsuranceCoverageDecision",
                table: "HR_SalarySlips",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeStateInsuranceNumber",
                table: "HR_SalarySlips",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeStateInsuranceRuleId",
                table: "HR_SalarySlips",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeStateInsuranceRuleVersion",
                table: "HR_SalarySlips",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EmployeeStateInsuranceWages",
                table: "HR_SalarySlips",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmployeeStateInsuranceContributionPeriodEnd",
                table: "HR_PayrollRunItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmployeeStateInsuranceContributionPeriodStart",
                table: "HR_PayrollRunItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeStateInsuranceCoverageDecision",
                table: "HR_PayrollRunItems",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeStateInsuranceNumber",
                table: "HR_PayrollRunItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeStateInsuranceRuleId",
                table: "HR_PayrollRunItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeStateInsuranceRuleVersion",
                table: "HR_PayrollRunItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EmployeeStateInsuranceWages",
                table: "HR_PayrollRunItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "AppEmployeeStatutoryEnrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemeCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WageCeilingCategory = table.Column<int>(type: "integer", nullable: false),
                    InsuranceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CoveredFrom = table.Column<DateTime>(type: "date", nullable: true),
                    CoveredTo = table.Column<DateTime>(type: "date", nullable: true),
                    ContributionPeriodEnd = table.Column<DateTime>(type: "date", nullable: true),
                    StatusReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AppEmployeeStatutoryEnrollments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPayrollComponentStatutoryTreatments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ComponentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemeCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Treatment = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPayrollComponentStatutoryTreatments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStatutoryEstablishments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SchemeCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JurisdictionCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmployeeCount = table.Column<int>(type: "integer", nullable: false),
                    CoveredSince = table.Column<DateTime>(type: "date", nullable: true),
                    BenefitsActiveFrom = table.Column<DateTime>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ConfirmedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppStatutoryEstablishments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStatutoryOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemeCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OverrideType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "date", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AppStatutoryOverrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStatutoryRuleVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SchemeCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JurisdictionCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "date", nullable: true),
                    EstablishmentThreshold = table.Column<int>(type: "integer", nullable: false),
                    StandardWageCeiling = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DisabilityWageCeiling = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EmployeePercentage = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    EmployerPercentage = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    ExclusionCapPercentage = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    InKindCapPercentage = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    RoundingPolicy = table.Column<int>(type: "integer", nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SourceVerifiedOn = table.Column<DateTime>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppStatutoryRuleVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployeeStatutoryEnrollments_TenantId_EmployeeId_SchemeC~",
                table: "AppEmployeeStatutoryEnrollments",
                columns: new[] { "TenantId", "EmployeeId", "SchemeCode" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppPayrollComponentStatutoryTreatments_TenantId_ComponentId~",
                table: "AppPayrollComponentStatutoryTreatments",
                columns: new[] { "TenantId", "ComponentId", "SchemeCode", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStatutoryEstablishments_TenantId_SchemeCode",
                table: "AppStatutoryEstablishments",
                columns: new[] { "TenantId", "SchemeCode" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppStatutoryOverrides_TenantId_EmployeeId_SchemeCode_Effect~",
                table: "AppStatutoryOverrides",
                columns: new[] { "TenantId", "EmployeeId", "SchemeCode", "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStatutoryRuleVersions_TenantId_SchemeCode_EffectiveFrom_~",
                table: "AppStatutoryRuleVersions",
                columns: new[] { "TenantId", "SchemeCode", "EffectiveFrom", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStatutoryRuleVersions_TenantId_SchemeCode_JurisdictionCo~",
                table: "AppStatutoryRuleVersions",
                columns: new[] { "TenantId", "SchemeCode", "JurisdictionCode", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppEmployeeStatutoryEnrollments");

            migrationBuilder.DropTable(
                name: "AppPayrollComponentStatutoryTreatments");

            migrationBuilder.DropTable(
                name: "AppStatutoryEstablishments");

            migrationBuilder.DropTable(
                name: "AppStatutoryOverrides");

            migrationBuilder.DropTable(
                name: "AppStatutoryRuleVersions");

            migrationBuilder.DropColumn(
                name: "EmployeeStateInsuranceContributionPeriodEnd",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "EmployeeStateInsuranceContributionPeriodStart",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "EmployeeStateInsuranceCoverageDecision",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "EmployeeStateInsuranceNumber",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "EmployeeStateInsuranceRuleId",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "EmployeeStateInsuranceRuleVersion",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "EmployeeStateInsuranceWages",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "EmployeeStateInsuranceContributionPeriodEnd",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "EmployeeStateInsuranceContributionPeriodStart",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "EmployeeStateInsuranceCoverageDecision",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "EmployeeStateInsuranceNumber",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "EmployeeStateInsuranceRuleId",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "EmployeeStateInsuranceRuleVersion",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "EmployeeStateInsuranceWages",
                table: "HR_PayrollRunItems");
        }
    }
}
