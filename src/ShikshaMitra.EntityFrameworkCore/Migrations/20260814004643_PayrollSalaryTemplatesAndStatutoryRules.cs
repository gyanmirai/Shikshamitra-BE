using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations;

public partial class PayrollSalaryTemplatesAndStatutoryRules : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("""
			DROP TABLE "AppProfessionalTaxes";
			DROP TABLE "AppProvidentFunds";

			ALTER TABLE "AppEmployeePayrollComponents"
				ADD COLUMN "SemanticType" integer NOT NULL DEFAULT 0,
				ADD COLUMN "StatutoryRuleId" uuid NULL;

			ALTER TABLE "AppEmployeePayrollDetails"
				ADD COLUMN "QuotedSalaryBasis" character varying(16) NULL,
				ADD COLUMN "QuotedMonthlyAmount" numeric(18,2) NULL,
				ADD COLUMN "SourceSalaryTemplateId" uuid NULL,
				ADD COLUMN "SourceSalaryTemplateVersionId" uuid NULL,
				ADD COLUMN "SourceSalaryTemplateVersion" integer NULL,
				ADD COLUMN "SalaryGeneratedAt" timestamp with time zone NULL,
				ADD COLUMN "WasModifiedAfterGeneration" boolean NOT NULL DEFAULT FALSE;

			ALTER TABLE "HR_PayrollRunItems"
				ADD COLUMN "EmployerContributionTotal" numeric NOT NULL DEFAULT 0,
				ADD COLUMN "EmployerContributionsJson" jsonb NOT NULL DEFAULT '[]'::jsonb,
				ADD COLUMN "ProvidentFundRuleId" uuid NULL,
				ADD COLUMN "ProvidentFundRuleVersion" integer NULL,
				ADD COLUMN "ProfessionalTaxRuleId" uuid NULL,
				ADD COLUMN "ProfessionalTaxRuleVersion" integer NULL;

			ALTER TABLE "HR_SalarySlips"
				ADD COLUMN "EmployerContributions" jsonb NOT NULL DEFAULT '[]'::jsonb,
				ADD COLUMN "EmployerContributionTotal" numeric(18,2) NOT NULL DEFAULT 0,
				ADD COLUMN "ProvidentFundRuleId" uuid NULL,
				ADD COLUMN "ProvidentFundRuleVersion" integer NULL,
				ADD COLUMN "ProfessionalTaxRuleId" uuid NULL,
				ADD COLUMN "ProfessionalTaxRuleVersion" integer NULL;

			CREATE TABLE "AppProvidentFunds" (
				"Id" uuid NOT NULL,
				"TenantId" uuid NULL,
				"Code" character varying(20) NOT NULL,
				"Name" character varying(100) NOT NULL,
				"EmployeePercentage" numeric(7,4) NOT NULL,
				"EmployerPercentage" numeric(7,4) NOT NULL,
				"ContributionBasis" integer NOT NULL,
				"WageCeiling" numeric(18,2) NULL,
				"EffectiveFrom" date NOT NULL,
				"EffectiveTo" date NULL,
				"IsActive" boolean NOT NULL,
				"Version" integer NOT NULL,
				"ExtraProperties" text NOT NULL,
				"ConcurrencyStamp" character varying(40) NOT NULL,
				"CreationTime" timestamp with time zone NOT NULL,
				"CreatorId" uuid NULL,
				"LastModificationTime" timestamp with time zone NULL,
				"LastModifierId" uuid NULL,
				"IsDeleted" boolean NOT NULL DEFAULT FALSE,
				"DeleterId" uuid NULL,
				"DeletionTime" timestamp with time zone NULL,
				CONSTRAINT "PK_AppProvidentFunds" PRIMARY KEY ("Id")
			);

			CREATE UNIQUE INDEX "IX_ProvidentFunds_TenantId_Code"
				ON "AppProvidentFunds" ("TenantId", "Code") WHERE "IsDeleted" = FALSE;
			CREATE UNIQUE INDEX "IX_ProvidentFunds_TenantId_Name"
				ON "AppProvidentFunds" ("TenantId", "Name") WHERE "IsDeleted" = FALSE;
			CREATE INDEX "IX_AppProvidentFunds_TenantId_EffectiveFrom_IsActive"
				ON "AppProvidentFunds" ("TenantId", "EffectiveFrom", "IsActive");

			CREATE TABLE "AppProfessionalTaxes" (
				"Id" uuid NOT NULL,
				"TenantId" uuid NULL,
				"JurisdictionCode" character varying(16) NOT NULL,
				"EffectiveFrom" date NOT NULL,
				"EffectiveTo" date NULL,
				"FromSalary" numeric(18,2) NOT NULL,
				"ToSalary" numeric(18,2) NULL,
				"TaxAmount" numeric(18,2) NOT NULL,
				"IsActive" boolean NOT NULL,
				"DeductInCessationMonth" boolean NOT NULL DEFAULT TRUE,
				"Version" integer NOT NULL,
				"ExtraProperties" text NOT NULL,
				"ConcurrencyStamp" character varying(40) NOT NULL,
				"CreationTime" timestamp with time zone NOT NULL,
				"CreatorId" uuid NULL,
				"LastModificationTime" timestamp with time zone NULL,
				"LastModifierId" uuid NULL,
				"IsDeleted" boolean NOT NULL DEFAULT FALSE,
				"DeleterId" uuid NULL,
				"DeletionTime" timestamp with time zone NULL,
				CONSTRAINT "PK_AppProfessionalTaxes" PRIMARY KEY ("Id")
			);

			CREATE UNIQUE INDEX "IX_AppProfessionalTaxes_TenantId_JurisdictionCode_EffectiveFro~"
				ON "AppProfessionalTaxes" ("TenantId", "JurisdictionCode", "EffectiveFrom", "FromSalary")
				WHERE "IsDeleted" = FALSE;

			CREATE TABLE "HR_SalaryStructureTemplates" (
				"Id" uuid NOT NULL,
				"TenantId" uuid NULL,
				"Name" character varying(160) NOT NULL,
				"Description" character varying(500) NULL,
				"Status" integer NOT NULL,
				"IsDefault" boolean NOT NULL,
				"LatestVersion" integer NOT NULL,
				"PublishedVersion" integer NULL,
				"ExtraProperties" text NOT NULL,
				"ConcurrencyStamp" character varying(40) NOT NULL,
				"CreationTime" timestamp with time zone NOT NULL,
				"CreatorId" uuid NULL,
				"LastModificationTime" timestamp with time zone NULL,
				"LastModifierId" uuid NULL,
				"IsDeleted" boolean NOT NULL DEFAULT FALSE,
				"DeleterId" uuid NULL,
				"DeletionTime" timestamp with time zone NULL,
				CONSTRAINT "PK_HR_SalaryStructureTemplates" PRIMARY KEY ("Id")
			);

			CREATE UNIQUE INDEX "IX_HR_SalaryStructureTemplates_TenantId"
				ON "HR_SalaryStructureTemplates" ("TenantId")
				WHERE "IsDeleted" = FALSE AND "IsDefault" = TRUE;
			CREATE UNIQUE INDEX "IX_HR_SalaryStructureTemplates_TenantId_Name"
				ON "HR_SalaryStructureTemplates" ("TenantId", "Name") WHERE "IsDeleted" = FALSE;

			CREATE TABLE "HR_SalaryStructureTemplateVersions" (
				"Id" uuid NOT NULL,
				"TenantId" uuid NULL,
				"TemplateId" uuid NOT NULL,
				"VersionNumber" integer NOT NULL,
				"Status" integer NOT NULL,
				"RulesJson" jsonb NOT NULL,
				"PublishedAt" timestamp with time zone NULL,
				"ExtraProperties" text NOT NULL,
				"ConcurrencyStamp" character varying(40) NOT NULL,
				"CreationTime" timestamp with time zone NOT NULL,
				"CreatorId" uuid NULL,
				"LastModificationTime" timestamp with time zone NULL,
				"LastModifierId" uuid NULL,
				"IsDeleted" boolean NOT NULL DEFAULT FALSE,
				"DeleterId" uuid NULL,
				"DeletionTime" timestamp with time zone NULL,
				CONSTRAINT "PK_HR_SalaryStructureTemplateVersions" PRIMARY KEY ("Id"),
				CONSTRAINT "FK_HR_SalaryStructureTemplateVersions_HR_SalaryStructureTempla~"
					FOREIGN KEY ("TemplateId") REFERENCES "HR_SalaryStructureTemplates" ("Id") ON DELETE RESTRICT
			);

			CREATE INDEX "IX_HR_SalaryStructureTemplateVersions_TemplateId"
				ON "HR_SalaryStructureTemplateVersions" ("TemplateId");
			CREATE INDEX "IX_HR_SalaryStructureTemplateVersions_TenantId_TemplateId_Stat~"
				ON "HR_SalaryStructureTemplateVersions" ("TenantId", "TemplateId", "Status");
			CREATE UNIQUE INDEX "IX_HR_SalaryStructureTemplateVersions_TenantId_TemplateId_Vers~"
				ON "HR_SalaryStructureTemplateVersions" ("TenantId", "TemplateId", "VersionNumber")
				WHERE "IsDeleted" = FALSE;
			""");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("""
			DROP TABLE "HR_SalaryStructureTemplateVersions";
			DROP TABLE "HR_SalaryStructureTemplates";
			DROP TABLE "AppProfessionalTaxes";
			DROP TABLE "AppProvidentFunds";

			ALTER TABLE "HR_SalarySlips"
				DROP COLUMN "EmployerContributions",
				DROP COLUMN "EmployerContributionTotal",
				DROP COLUMN "ProvidentFundRuleId",
				DROP COLUMN "ProvidentFundRuleVersion",
				DROP COLUMN "ProfessionalTaxRuleId",
				DROP COLUMN "ProfessionalTaxRuleVersion";

			ALTER TABLE "HR_PayrollRunItems"
				DROP COLUMN "EmployerContributionTotal",
				DROP COLUMN "EmployerContributionsJson",
				DROP COLUMN "ProvidentFundRuleId",
				DROP COLUMN "ProvidentFundRuleVersion",
				DROP COLUMN "ProfessionalTaxRuleId",
				DROP COLUMN "ProfessionalTaxRuleVersion";

			ALTER TABLE "AppEmployeePayrollDetails"
				DROP COLUMN "QuotedSalaryBasis",
				DROP COLUMN "QuotedMonthlyAmount",
				DROP COLUMN "SourceSalaryTemplateId",
				DROP COLUMN "SourceSalaryTemplateVersionId",
				DROP COLUMN "SourceSalaryTemplateVersion",
				DROP COLUMN "SalaryGeneratedAt",
				DROP COLUMN "WasModifiedAfterGeneration";

			ALTER TABLE "AppEmployeePayrollComponents"
				DROP COLUMN "SemanticType",
				DROP COLUMN "StatutoryRuleId";

			CREATE TABLE "AppProvidentFunds" (
				"Id" uuid NOT NULL,
				"TenantId" uuid NULL,
				"Code" character varying(20) NOT NULL,
				"Name" character varying(100) NOT NULL,
				"EmployeePercentage" real NOT NULL,
				"EmployerPercentage" real NOT NULL,
				"ExtraProperties" text NOT NULL,
				"ConcurrencyStamp" character varying(40) NOT NULL,
				"CreationTime" timestamp with time zone NOT NULL,
				"CreatorId" uuid NULL,
				"LastModificationTime" timestamp with time zone NULL,
				"LastModifierId" uuid NULL,
				"IsDeleted" boolean NOT NULL DEFAULT FALSE,
				"DeleterId" uuid NULL,
				"DeletionTime" timestamp with time zone NULL,
				CONSTRAINT "PK_AppProvidentFunds" PRIMARY KEY ("Id")
			);

			CREATE UNIQUE INDEX "IX_ProvidentFunds_TenantId_Code"
				ON "AppProvidentFunds" ("TenantId", "Code") WHERE "IsDeleted" = FALSE;
			CREATE UNIQUE INDEX "IX_ProvidentFunds_TenantId_Name"
				ON "AppProvidentFunds" ("TenantId", "Name") WHERE "IsDeleted" = FALSE;

			CREATE TABLE "AppProfessionalTaxes" (
				"Id" uuid NOT NULL,
				"TenantId" uuid NULL,
				"GenderType" integer NOT NULL,
				"FromSalary" double precision NOT NULL,
				"ToSalary" double precision NOT NULL,
				"TaxAmount" double precision NOT NULL,
				"ExtraProperties" text NOT NULL,
				"ConcurrencyStamp" character varying(40) NOT NULL,
				"CreationTime" timestamp with time zone NOT NULL,
				"CreatorId" uuid NULL,
				"LastModificationTime" timestamp with time zone NULL,
				"LastModifierId" uuid NULL,
				"IsDeleted" boolean NOT NULL DEFAULT FALSE,
				"DeleterId" uuid NULL,
				"DeletionTime" timestamp with time zone NULL,
				CONSTRAINT "PK_AppProfessionalTaxes" PRIMARY KEY ("Id")
			);
			""");
	}
}
