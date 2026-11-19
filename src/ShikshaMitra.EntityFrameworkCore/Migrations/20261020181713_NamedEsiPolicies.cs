using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class NamedEsiPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppStatutoryRuleVersions_TenantId_SchemeCode_JurisdictionCo~",
                table: "AppStatutoryRuleVersions");

            migrationBuilder.AddColumn<string>(
                name: "DefaultEsiExternalReason",
                table: "HR_SalaryStructureTemplateVersions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultEsiManagementMode",
                table: "HR_SalaryStructureTemplateVersions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "IN_PLATFORM");

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultEsiPolicyId",
                table: "HR_SalaryStructureTemplateVersions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EsiExternalReason",
                table: "HR_EmployeeCompensationRevisions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EsiManagementMode",
                table: "HR_EmployeeCompensationRevisions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "IN_PLATFORM");

            migrationBuilder.AddColumn<Guid>(
                name: "EsiPolicyId",
                table: "HR_EmployeeCompensationRevisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalculationSettingsJson",
                table: "AppStatutoryRuleVersions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EsiPolicyId",
                table: "AppStatutoryRuleVersions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                table: "AppStatutoryRuleVersions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppEsiPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppEsiPolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HR_SalaryStructureTemplateVersions_DefaultEsiPolicyId",
                table: "HR_SalaryStructureTemplateVersions",
                column: "DefaultEsiPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_EmployeeCompensationRevisions_EsiPolicyId",
                table: "HR_EmployeeCompensationRevisions",
                column: "EsiPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStatutoryRuleVersions_EsiPolicyId",
                table: "AppStatutoryRuleVersions",
                column: "EsiPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStatutoryRuleVersions_TenantId_EsiPolicyId_EffectiveFrom",
                table: "AppStatutoryRuleVersions",
                columns: new[] { "TenantId", "EsiPolicyId", "EffectiveFrom" },
                unique: true,
                filter: "\"EsiPolicyId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppStatutoryRuleVersions_TenantId_EsiPolicyId_Version",
                table: "AppStatutoryRuleVersions",
                columns: new[] { "TenantId", "EsiPolicyId", "Version" },
                unique: true,
                filter: "\"EsiPolicyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppStatutoryRuleVersions_TenantId_SchemeCode_JurisdictionCo~",
                table: "AppStatutoryRuleVersions",
                columns: new[] { "TenantId", "SchemeCode", "JurisdictionCode", "Version" },
                unique: true,
                filter: "\"EsiPolicyId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppEsiPolicies_TenantId",
                table: "AppEsiPolicies",
                column: "TenantId",
                unique: true,
                filter: "\"IsDefault\" = true AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppEsiPolicies_TenantId_Code",
                table: "AppEsiPolicies",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStatutoryRuleVersions_AppEsiPolicies_EsiPolicyId",
                table: "AppStatutoryRuleVersions",
                column: "EsiPolicyId",
                principalTable: "AppEsiPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HR_EmployeeCompensationRevisions_AppEsiPolicies_EsiPolicyId",
                table: "HR_EmployeeCompensationRevisions",
                column: "EsiPolicyId",
                principalTable: "AppEsiPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HR_SalaryStructureTemplateVersions_AppEsiPolicies_DefaultEs~",
                table: "HR_SalaryStructureTemplateVersions",
                column: "DefaultEsiPolicyId",
                principalTable: "AppEsiPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                INSERT INTO "AppEsiPolicies" ("Id", "TenantId", "Name", "Code", "IsArchived", "IsDefault", "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted")
                SELECT md5(coalesce(t."TenantId"::text, 'host') || ':STANDARD_ESIC')::uuid, t."TenantId", 'Standard ESIC', 'STANDARD_ESIC', false, true, '{}', md5(coalesce(t."TenantId"::text, 'host') || ':ESIC_POLICY'), now(), false
                FROM (SELECT DISTINCT "TenantId" FROM "AppStatutoryRuleVersions" WHERE "SchemeCode" = 'ESI' AND NOT "IsDeleted"
                      UNION SELECT DISTINCT "TenantId" FROM "HR_SalaryStructureTemplateVersions" WHERE NOT "IsDeleted"
                      UNION SELECT DISTINCT "TenantId" FROM "HR_EmployeeCompensationRevisions" WHERE NOT "IsDeleted") t;

                -- Ambiguous legacy dates remain unassigned and require administrator setup.
                UPDATE "AppStatutoryRuleVersions" r SET "EsiPolicyId" = p."Id"
                FROM "AppEsiPolicies" p
                WHERE r."TenantId" IS NOT DISTINCT FROM p."TenantId" AND r."SchemeCode" = 'ESI' AND NOT r."IsDeleted"
                  AND NOT EXISTS (SELECT 1 FROM "AppStatutoryRuleVersions" a
                    WHERE a."TenantId" IS NOT DISTINCT FROM p."TenantId" AND a."SchemeCode" = 'ESI' AND NOT a."IsDeleted"
                    GROUP BY a."EffectiveFrom" HAVING count(*) > 1);

                UPDATE "HR_SalaryStructureTemplateVersions" v SET "DefaultEsiPolicyId" = p."Id"
                FROM "AppEsiPolicies" p WHERE v."TenantId" IS NOT DISTINCT FROM p."TenantId"
                  AND EXISTS (SELECT 1 FROM "AppStatutoryRuleVersions" r WHERE r."EsiPolicyId" = p."Id");
                UPDATE "HR_EmployeeCompensationRevisions" v SET "EsiPolicyId" = p."Id"
                FROM "AppEsiPolicies" p WHERE v."TenantId" IS NOT DISTINCT FROM p."TenantId"
                  AND EXISTS (SELECT 1 FROM "AppStatutoryRuleVersions" r WHERE r."EsiPolicyId" = p."Id");
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStatutoryRuleVersions_AppEsiPolicies_EsiPolicyId",
                table: "AppStatutoryRuleVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_HR_EmployeeCompensationRevisions_AppEsiPolicies_EsiPolicyId",
                table: "HR_EmployeeCompensationRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_HR_SalaryStructureTemplateVersions_AppEsiPolicies_DefaultEs~",
                table: "HR_SalaryStructureTemplateVersions");

            migrationBuilder.DropTable(
                name: "AppEsiPolicies");

            migrationBuilder.DropIndex(
                name: "IX_HR_SalaryStructureTemplateVersions_DefaultEsiPolicyId",
                table: "HR_SalaryStructureTemplateVersions");

            migrationBuilder.DropIndex(
                name: "IX_HR_EmployeeCompensationRevisions_EsiPolicyId",
                table: "HR_EmployeeCompensationRevisions");

            migrationBuilder.DropIndex(
                name: "IX_AppStatutoryRuleVersions_EsiPolicyId",
                table: "AppStatutoryRuleVersions");

            migrationBuilder.DropIndex(
                name: "IX_AppStatutoryRuleVersions_TenantId_EsiPolicyId_EffectiveFrom",
                table: "AppStatutoryRuleVersions");

            migrationBuilder.DropIndex(
                name: "IX_AppStatutoryRuleVersions_TenantId_EsiPolicyId_Version",
                table: "AppStatutoryRuleVersions");

            migrationBuilder.DropIndex(
                name: "IX_AppStatutoryRuleVersions_TenantId_SchemeCode_JurisdictionCo~",
                table: "AppStatutoryRuleVersions");

            migrationBuilder.DropColumn(
                name: "DefaultEsiExternalReason",
                table: "HR_SalaryStructureTemplateVersions");

            migrationBuilder.DropColumn(
                name: "DefaultEsiManagementMode",
                table: "HR_SalaryStructureTemplateVersions");

            migrationBuilder.DropColumn(
                name: "DefaultEsiPolicyId",
                table: "HR_SalaryStructureTemplateVersions");

            migrationBuilder.DropColumn(
                name: "EsiExternalReason",
                table: "HR_EmployeeCompensationRevisions");

            migrationBuilder.DropColumn(
                name: "EsiManagementMode",
                table: "HR_EmployeeCompensationRevisions");

            migrationBuilder.DropColumn(
                name: "EsiPolicyId",
                table: "HR_EmployeeCompensationRevisions");

            migrationBuilder.DropColumn(
                name: "CalculationSettingsJson",
                table: "AppStatutoryRuleVersions");

            migrationBuilder.DropColumn(
                name: "EsiPolicyId",
                table: "AppStatutoryRuleVersions");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                table: "AppStatutoryRuleVersions");

            migrationBuilder.CreateIndex(
                name: "IX_AppStatutoryRuleVersions_TenantId_SchemeCode_JurisdictionCo~",
                table: "AppStatutoryRuleVersions",
                columns: new[] { "TenantId", "SchemeCode", "JurisdictionCode", "Version" },
                unique: true);
        }
    }
}
