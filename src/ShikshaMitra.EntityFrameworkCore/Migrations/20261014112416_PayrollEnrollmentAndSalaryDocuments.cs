using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class PayrollEnrollmentAndSalaryDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StatutoryApplicationJson",
                table: "HR_SalarySlips",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PresentationJson",
                table: "HR_PayslipSettings",
                type: "text",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<Guid>(
                name: "PresentationVersionId",
                table: "HR_PayslipSettings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "LeaveUnitsByTypeJson",
                table: "HR_PayrollRunItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatutoryApplicationJson",
                table: "HR_PayrollRunItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayrollCategory",
                table: "AppEmployeeLeavePolicyRules",
                type: "text",
                nullable: false,
                defaultValue: "PAID");

            migrationBuilder.CreateTable(
                name: "HR_EmployeeStatutoryProfileVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveMonth = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApplyPf = table.Column<bool>(type: "boolean", nullable: false),
                    PfNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UanNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PfDisabledReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApplyEsic = table.Column<bool>(type: "boolean", nullable: false),
                    EsicInsuranceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EsicDisabledReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsSuperseded = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_HR_EmployeeStatutoryProfileVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HR_EmployeeStatutoryProfileVersions_TenantId_EmployeeId_Eff~",
                table: "HR_EmployeeStatutoryProfileVersions",
                columns: new[] { "TenantId", "EmployeeId", "EffectiveMonth" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"IsSuperseded\" = false")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_HR_EmployeeStatutoryProfileVersions_TenantId_EmployeeId_Req~",
                table: "HR_EmployeeStatutoryProfileVersions",
                columns: new[] { "TenantId", "EmployeeId", "RequestId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
            migrationBuilder.Sql("""
                UPDATE "AppEmployeeLeavePolicyRules" SET "PayrollCategory" = 'UNPAID' WHERE "PaidType" = 1;
                UPDATE "HR_PayslipSettings" SET "PresentationVersionId" = gen_random_uuid();
                INSERT INTO "HR_EmployeeStatutoryProfileVersions"
                    ("Id", "TenantId", "EmployeeId", "EffectiveMonth", "ApplyPf", "PfNumber", "UanNumber", "PfDisabledReason",
                     "ApplyEsic", "EsicInsuranceNumber", "RequestId", "RequestFingerprint", "IsSuperseded", "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted")
                SELECT gen_random_uuid(), e."TenantId", e."Id",
                    date_trunc('month', CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Kolkata') AT TIME ZONE 'UTC',
                    CASE WHEN c."Id" IS NOT NULL THEN c."PfRuleId" IS NOT NULL ELSE true END,
                    NULLIF(NULLIF(btrim(d."EpfNumber"), ''), '0'), NULLIF(NULLIF(btrim(d."UanNumber"), ''), '0'),
                    CASE WHEN c."Id" IS NOT NULL AND c."PfRuleId" IS NULL THEN 'Existing salary setup has PF turned off.' ELSE NULL END,
                    true, NULLIF(NULLIF(btrim(esi."InsuranceNumber"), ''), '0'), gen_random_uuid(), 'MIGRATED', false, '{}', gen_random_uuid()::text, CURRENT_TIMESTAMP, false
                FROM "AppEmployees" e
                LEFT JOIN "AppEmployeePayrollDetails" d ON d."EmployeeId" = e."Id"
                LEFT JOIN "AppEmployeeStatutoryEnrollments" esi ON esi."EmployeeId" = e."Id" AND esi."TenantId" IS NOT DISTINCT FROM e."TenantId" AND esi."SchemeCode" = 'ESI' AND NOT esi."IsDeleted"
                LEFT JOIN LATERAL (SELECT r."Id", r."PfRuleId" FROM "HR_EmployeeCompensationRevisions" r
                    WHERE r."EmployeeId" = e."Id" AND r."TenantId" IS NOT DISTINCT FROM e."TenantId" AND r."Status" = 2 AND NOT r."IsDeleted"
                    AND r."EffectiveMonth" <= date_trunc('month', CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Kolkata') AT TIME ZONE 'UTC'
                    ORDER BY r."EffectiveMonth" DESC, r."RevisionNumber" DESC LIMIT 1) c ON true
                WHERE NOT e."IsDeleted" AND (d."Id" IS NOT NULL OR esi."Id" IS NOT NULL OR c."Id" IS NOT NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HR_EmployeeStatutoryProfileVersions");

            migrationBuilder.DropColumn(
                name: "StatutoryApplicationJson",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "PresentationJson",
                table: "HR_PayslipSettings");

            migrationBuilder.DropColumn(
                name: "PresentationVersionId",
                table: "HR_PayslipSettings");

            migrationBuilder.DropColumn(
                name: "LeaveUnitsByTypeJson",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "StatutoryApplicationJson",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "PayrollCategory",
                table: "AppEmployeeLeavePolicyRules");

        }
    }
}
