using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260818170000_AddAdmissionWorkflowRuntimeHistory")]
public partial class AddAdmissionWorkflowRuntimeHistory : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<DateTime>(
			name: "CompletedAt",
			table: "AppAdmissionApplicationWorkflowInstances",
			type: "timestamp with time zone",
			nullable: true);

		migrationBuilder.AddColumn<int>(
			name: "Status",
			table: "AppAdmissionApplicationWorkflowInstances",
			type: "integer",
			nullable: false,
			defaultValue: 0);

		migrationBuilder.AddColumn<Guid>(
			name: "TenantId",
			table: "AppAdmissionApplicationStageInstances",
			type: "uuid",
			nullable: true);

		migrationBuilder.AddColumn<Guid>(
			name: "CompletedBy",
			table: "AppAdmissionApplicationStageInstances",
			type: "uuid",
			nullable: true);

		migrationBuilder.AddColumn<string>(
			name: "CompletionReason",
			table: "AppAdmissionApplicationStageInstances",
			type: "character varying(512)",
			maxLength: 512,
			nullable: true);

		migrationBuilder.Sql(
			"""
			UPDATE "AppAdmissionApplicationStageInstances" AS stage
			SET "TenantId" = instance."TenantId"
			FROM "AppAdmissionApplicationWorkflowInstances" AS instance
			WHERE stage."WorkflowInstanceId" = instance."Id";
			""");

		migrationBuilder.CreateTable(
			name: "AppAdmissionWorkflowTransitionHistory",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "uuid", nullable: false),
				TenantId = table.Column<Guid>(type: "uuid", nullable: true),
				ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
				WorkflowInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
				FromStageInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
				ToStageInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
				TransitionDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
				Action = table.Column<int>(type: "integer", nullable: false),
				IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
				Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
				ConditionEvaluationSnapshotJson = table.Column<string>(type: "text", nullable: true),
				OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
				ExtraProperties = table.Column<string>(type: "text", nullable: false),
				ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
				CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
				CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
				LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
				LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
				IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
				DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
				DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_AppAdmissionWorkflowTransitionHistory", item => item.Id);
				table.ForeignKey(
					name: "FK_AdmissionWorkflowTransitionHistory_Applications",
					column: item => item.ApplicationId,
					principalTable: "AppAdmissionApplications",
					principalColumn: "Id",
					onDelete: ReferentialAction.Restrict);
				table.ForeignKey(
					name: "FK_AdmissionWorkflowTransitionHistory_WorkflowInstances",
					column: item => item.WorkflowInstanceId,
					principalTable: "AppAdmissionApplicationWorkflowInstances",
					principalColumn: "Id",
					onDelete: ReferentialAction.Cascade);
			});

		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionApplicationWorkflowInstances_TenantId_Status",
			table: "AppAdmissionApplicationWorkflowInstances",
			columns: new[] { "TenantId", "Status" });

		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionApplicationWorkflowInstances_WorkflowDefinitionId",
			table: "AppAdmissionApplicationWorkflowInstances",
			column: "WorkflowDefinitionId");

		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionApplicationWorkflowInstances_ApplicationId",
			table: "AppAdmissionApplicationWorkflowInstances",
			column: "ApplicationId");

		migrationBuilder.DropIndex(
			name: "IX_AppAdmissionApplicationStageInstances_WorkflowInstanceId_DisplayOrder",
			table: "AppAdmissionApplicationStageInstances");

		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionApplicationStageInstances_TenantId_WorkflowInstanceId_DisplayOrder",
			table: "AppAdmissionApplicationStageInstances",
			columns: new[] { "TenantId", "WorkflowInstanceId", "DisplayOrder" });

		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionApplicationStageInstances_DefinitionStageId",
			table: "AppAdmissionApplicationStageInstances",
			column: "DefinitionStageId");

		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionApplicationStageInstances_WorkflowInstanceId",
			table: "AppAdmissionApplicationStageInstances",
			column: "WorkflowInstanceId");

		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionFormInstances_FormDefinitionId",
			table: "AppAdmissionFormInstances",
			column: "FormDefinitionId");

		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionFormInstances_ApplicationId",
			table: "AppAdmissionFormInstances",
			column: "ApplicationId");

		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionWorkflowTransitionHistory_ApplicationId",
			table: "AppAdmissionWorkflowTransitionHistory",
			column: "ApplicationId");

		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionWorkflowTransitionHistory_WorkflowInstanceId",
			table: "AppAdmissionWorkflowTransitionHistory",
			column: "WorkflowInstanceId");

		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionWorkflowTransitionHistory_TenantId_ApplicationId_OccurredAt",
			table: "AppAdmissionWorkflowTransitionHistory",
			columns: new[] { "TenantId", "ApplicationId", "OccurredAt" });

		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionWorkflowTransitionHistory_TenantId_WorkflowInstanceId_IdempotencyKey",
			table: "AppAdmissionWorkflowTransitionHistory",
			columns: new[] { "TenantId", "WorkflowInstanceId", "IdempotencyKey" },
			unique: true,
			filter: "\"IsDeleted\" = false");

		migrationBuilder.AddForeignKey(
			name: "FK_AdmissionWorkflowInstances_Applications",
			table: "AppAdmissionApplicationWorkflowInstances",
			column: "ApplicationId",
			principalTable: "AppAdmissionApplications",
			principalColumn: "Id",
			onDelete: ReferentialAction.Restrict);

		migrationBuilder.AddForeignKey(
			name: "FK_AdmissionWorkflowInstances_Definitions",
			table: "AppAdmissionApplicationWorkflowInstances",
			column: "WorkflowDefinitionId",
			principalTable: "AppAdmissionWorkflowDefinitions",
			principalColumn: "Id",
			onDelete: ReferentialAction.Restrict);

		migrationBuilder.AddForeignKey(
			name: "FK_AdmissionStageInstances_WorkflowInstances",
			table: "AppAdmissionApplicationStageInstances",
			column: "WorkflowInstanceId",
			principalTable: "AppAdmissionApplicationWorkflowInstances",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade);

		migrationBuilder.AddForeignKey(
			name: "FK_AdmissionStageInstances_Definitions",
			table: "AppAdmissionApplicationStageInstances",
			column: "DefinitionStageId",
			principalTable: "AppAdmissionWorkflowStages",
			principalColumn: "Id",
			onDelete: ReferentialAction.Restrict);

		migrationBuilder.AddForeignKey(
			name: "FK_AdmissionFormInstances_Applications",
			table: "AppAdmissionFormInstances",
			column: "ApplicationId",
			principalTable: "AppAdmissionApplications",
			principalColumn: "Id",
			onDelete: ReferentialAction.Restrict);

		migrationBuilder.AddForeignKey(
			name: "FK_AdmissionFormInstances_Definitions",
			table: "AppAdmissionFormInstances",
			column: "FormDefinitionId",
			principalTable: "AppAdmissionFormDefinitions",
			principalColumn: "Id",
			onDelete: ReferentialAction.Restrict);

		migrationBuilder.AddForeignKey(
			name: "FK_AdmissionFormValues_Instances",
			table: "AppAdmissionFormFieldValues",
			column: "FormInstanceId",
			principalTable: "AppAdmissionFormInstances",
			principalColumn: "Id",
			onDelete: ReferentialAction.Cascade);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropForeignKey(name: "FK_AdmissionWorkflowInstances_Applications", table: "AppAdmissionApplicationWorkflowInstances");
		migrationBuilder.DropForeignKey(name: "FK_AdmissionWorkflowInstances_Definitions", table: "AppAdmissionApplicationWorkflowInstances");
		migrationBuilder.DropForeignKey(name: "FK_AdmissionStageInstances_WorkflowInstances", table: "AppAdmissionApplicationStageInstances");
		migrationBuilder.DropForeignKey(name: "FK_AdmissionStageInstances_Definitions", table: "AppAdmissionApplicationStageInstances");
		migrationBuilder.DropForeignKey(name: "FK_AdmissionFormInstances_Applications", table: "AppAdmissionFormInstances");
		migrationBuilder.DropForeignKey(name: "FK_AdmissionFormInstances_Definitions", table: "AppAdmissionFormInstances");
		migrationBuilder.DropForeignKey(name: "FK_AdmissionFormValues_Instances", table: "AppAdmissionFormFieldValues");
		migrationBuilder.DropTable(name: "AppAdmissionWorkflowTransitionHistory");
		migrationBuilder.DropIndex(
			name: "IX_AppAdmissionApplicationWorkflowInstances_TenantId_Status",
			table: "AppAdmissionApplicationWorkflowInstances");
		migrationBuilder.DropColumn(name: "CompletedAt", table: "AppAdmissionApplicationWorkflowInstances");
		migrationBuilder.DropColumn(name: "Status", table: "AppAdmissionApplicationWorkflowInstances");
		migrationBuilder.DropIndex(name: "IX_AppAdmissionApplicationWorkflowInstances_WorkflowDefinitionId", table: "AppAdmissionApplicationWorkflowInstances");
		migrationBuilder.DropIndex(name: "IX_AppAdmissionApplicationWorkflowInstances_ApplicationId", table: "AppAdmissionApplicationWorkflowInstances");
		migrationBuilder.DropIndex(name: "IX_AppAdmissionApplicationStageInstances_TenantId_WorkflowInstanceId_DisplayOrder", table: "AppAdmissionApplicationStageInstances");
		migrationBuilder.DropIndex(name: "IX_AppAdmissionApplicationStageInstances_DefinitionStageId", table: "AppAdmissionApplicationStageInstances");
		migrationBuilder.DropIndex(name: "IX_AppAdmissionApplicationStageInstances_WorkflowInstanceId", table: "AppAdmissionApplicationStageInstances");
		migrationBuilder.DropIndex(name: "IX_AppAdmissionFormInstances_FormDefinitionId", table: "AppAdmissionFormInstances");
		migrationBuilder.DropIndex(name: "IX_AppAdmissionFormInstances_ApplicationId", table: "AppAdmissionFormInstances");
		migrationBuilder.DropColumn(name: "TenantId", table: "AppAdmissionApplicationStageInstances");
		migrationBuilder.DropColumn(name: "CompletedBy", table: "AppAdmissionApplicationStageInstances");
		migrationBuilder.DropColumn(name: "CompletionReason", table: "AppAdmissionApplicationStageInstances");
		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionApplicationStageInstances_WorkflowInstanceId_DisplayOrder",
			table: "AppAdmissionApplicationStageInstances",
			columns: new[] { "WorkflowInstanceId", "DisplayOrder" });
	}
}
