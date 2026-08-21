using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260818113000_ProductionizeAdmissionWorkflowEngine")]
public partial class ProductionizeAdmissionWorkflowEngine : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<string>(
			name: "Description",
			table: "AppAdmissionWorkflowDefinitions",
			type: "character varying(1024)",
			maxLength: 1024,
			nullable: true);
		migrationBuilder.AddColumn<bool>(
			name: "IsEntryStage",
			table: "AppAdmissionWorkflowStages",
			type: "boolean",
			nullable: false,
			defaultValue: false);
		migrationBuilder.AddColumn<bool>(
			name: "IsTerminalStage",
			table: "AppAdmissionWorkflowStages",
			type: "boolean",
			nullable: false,
			defaultValue: false);
		migrationBuilder.AddColumn<string>(
			name: "ConditionRuleJson",
			table: "AppAdmissionWorkflowTransitions",
			type: "character varying(8000)",
			maxLength: 8000,
			nullable: true);
		migrationBuilder.AddColumn<bool>(
			name: "IsDefault",
			table: "AppAdmissionWorkflowTransitions",
			type: "boolean",
			nullable: false,
			defaultValue: false);

		// Translate the former order-based convention into explicit graph semantics once.
		migrationBuilder.Sql("""
			UPDATE "AppAdmissionWorkflowStages" AS stage
			SET "IsEntryStage" = true
			WHERE stage."Id" = (
				SELECT candidate."Id"
				FROM "AppAdmissionWorkflowStages" AS candidate
				WHERE candidate."WorkflowDefinitionId" = stage."WorkflowDefinitionId"
				ORDER BY candidate."DisplayOrder", candidate."Id"
				LIMIT 1
			)
			""");
		migrationBuilder.Sql("""
			UPDATE "AppAdmissionWorkflowStages" AS stage
			SET "IsTerminalStage" = true
			WHERE stage."Id" = (
				SELECT candidate."Id"
				FROM "AppAdmissionWorkflowStages" AS candidate
				WHERE candidate."WorkflowDefinitionId" = stage."WorkflowDefinitionId"
				ORDER BY candidate."DisplayOrder" DESC, candidate."Id" DESC
				LIMIT 1
			)
			""");

		migrationBuilder.DropIndex(
			name: "IX_AppAdmissionWorkflowDefinitions_TenantId_Code_Version",
			table: "AppAdmissionWorkflowDefinitions");
		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionWorkflowDefinitions_TenantId_Code_Version",
			table: "AppAdmissionWorkflowDefinitions",
			columns: new[] { "TenantId", "Code", "Version" },
			unique: true,
			filter: "\"IsDeleted\" = false");
		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionWorkflowDefinitions_TenantId_Status",
			table: "AppAdmissionWorkflowDefinitions",
			columns: new[] { "TenantId", "Status" });
		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionWorkflowDefinitions_ParentVersionId",
			table: "AppAdmissionWorkflowDefinitions",
			column: "ParentVersionId");
		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionWorkflowStages_WorkflowDefinitionId_IsEntryStage",
			table: "AppAdmissionWorkflowStages",
			columns: new[] { "WorkflowDefinitionId", "IsEntryStage" });
		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionWorkflowStages_WorkflowDefinitionId_IsTerminalStage",
			table: "AppAdmissionWorkflowStages",
			columns: new[] { "WorkflowDefinitionId", "IsTerminalStage" });

		// PostgreSQL truncates identifiers to 63 bytes. The original From/To index can
		// therefore retain the truncated physical name when this migration is retried
		// after a partial/manual repair. Drop both possible physical names first, then
		// recreate the canonical short name. This is safe because the index is rebuilt
		// in the same migration and makes the operation idempotent.
		migrationBuilder.Sql("""
			DROP INDEX IF EXISTS "IX_AppAdmissionWorkflowTransitions_WorkflowDefinitionId_FromSta";
			DROP INDEX IF EXISTS "UX_AdmWfTransitions_Workflow_From_To";
			CREATE UNIQUE INDEX IF NOT EXISTS "UX_AdmWfTransitions_Workflow_From_To"
				ON "AppAdmissionWorkflowTransitions" ("WorkflowDefinitionId", "FromStageId", "ToStageId");
			""");
		migrationBuilder.CreateIndex(
			name: "IX_AdmWfTransitions_Workflow_From_Priority",
			table: "AppAdmissionWorkflowTransitions",
			columns: new[] { "WorkflowDefinitionId", "FromStageId", "Priority" });
		migrationBuilder.CreateIndex(
			name: "UX_AdmWfTransitions_Workflow_From_Default",
			table: "AppAdmissionWorkflowTransitions",
			columns: new[] { "WorkflowDefinitionId", "FromStageId", "IsDefault" },
			unique: true,
			filter: "\"IsDefault\" = true");

		migrationBuilder.AddForeignKey(
			name: "FK_AdmissionWorkflowStages_Definitions",
			table: "AppAdmissionWorkflowStages",
			column: "WorkflowDefinitionId",
			principalTable: "AppAdmissionWorkflowDefinitions",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade);
		migrationBuilder.AddForeignKey(
			name: "FK_AdmissionWorkflowTransitions_Definitions",
			table: "AppAdmissionWorkflowTransitions",
			column: "WorkflowDefinitionId",
			principalTable: "AppAdmissionWorkflowDefinitions",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade);
		migrationBuilder.AddForeignKey(
			name: "FK_AdmissionWorkflowTransitions_FromStages",
			table: "AppAdmissionWorkflowTransitions",
			column: "FromStageId",
			principalTable: "AppAdmissionWorkflowStages",
			principalColumn: "Id",
			onDelete: ReferentialAction.Restrict);
		migrationBuilder.AddForeignKey(
			name: "FK_AdmissionWorkflowTransitions_ToStages",
			table: "AppAdmissionWorkflowTransitions",
			column: "ToStageId",
			principalTable: "AppAdmissionWorkflowStages",
			principalColumn: "Id",
			onDelete: ReferentialAction.Restrict);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropForeignKey("FK_AdmissionWorkflowTransitions_ToStages", "AppAdmissionWorkflowTransitions");
		migrationBuilder.DropForeignKey("FK_AdmissionWorkflowTransitions_FromStages", "AppAdmissionWorkflowTransitions");
		migrationBuilder.DropForeignKey("FK_AdmissionWorkflowTransitions_Definitions", "AppAdmissionWorkflowTransitions");
		migrationBuilder.DropForeignKey("FK_AdmissionWorkflowStages_Definitions", "AppAdmissionWorkflowStages");

		migrationBuilder.DropIndex("UX_AdmWfTransitions_Workflow_From_Default", "AppAdmissionWorkflowTransitions");
		migrationBuilder.DropIndex("IX_AdmWfTransitions_Workflow_From_Priority", "AppAdmissionWorkflowTransitions");
		migrationBuilder.Sql("""
			ALTER INDEX IF EXISTS "UX_AdmWfTransitions_Workflow_From_To"
			RENAME TO "IX_AppAdmissionWorkflowTransitions_WorkflowDefinitionId_FromSta";
			""");
		migrationBuilder.DropIndex("IX_AppAdmissionWorkflowStages_WorkflowDefinitionId_IsTerminalStage", "AppAdmissionWorkflowStages");
		migrationBuilder.DropIndex("IX_AppAdmissionWorkflowStages_WorkflowDefinitionId_IsEntryStage", "AppAdmissionWorkflowStages");
		migrationBuilder.DropIndex("IX_AppAdmissionWorkflowDefinitions_ParentVersionId", "AppAdmissionWorkflowDefinitions");
		migrationBuilder.DropIndex("IX_AppAdmissionWorkflowDefinitions_TenantId_Status", "AppAdmissionWorkflowDefinitions");
		migrationBuilder.DropIndex("IX_AppAdmissionWorkflowDefinitions_TenantId_Code_Version", "AppAdmissionWorkflowDefinitions");
		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionWorkflowDefinitions_TenantId_Code_Version",
			table: "AppAdmissionWorkflowDefinitions",
			columns: new[] { "TenantId", "Code", "Version" },
			unique: true);

		migrationBuilder.DropColumn("IsDefault", "AppAdmissionWorkflowTransitions");
		migrationBuilder.DropColumn("ConditionRuleJson", "AppAdmissionWorkflowTransitions");
		migrationBuilder.DropColumn("IsTerminalStage", "AppAdmissionWorkflowStages");
		migrationBuilder.DropColumn("IsEntryStage", "AppAdmissionWorkflowStages");
		migrationBuilder.DropColumn("Description", "AppAdmissionWorkflowDefinitions");
	}
}
