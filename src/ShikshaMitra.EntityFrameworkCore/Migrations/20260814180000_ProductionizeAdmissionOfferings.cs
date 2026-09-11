using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260814180000_ProductionizeAdmissionOfferings")]
public partial class ProductionizeAdmissionOfferings : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(
			name: "IX_AppAdmissionOfferings_TenantId_AdmissionCycleId_CampusId_BoardId_MediumId_StageId_StandardId",
			table: "AppAdmissionOfferings");
		migrationBuilder.DropIndex(
			name: "IX_AppAdmissionOfferings_TenantId_AdmissionCycleId_IsActive",
			table: "AppAdmissionOfferings");

		migrationBuilder.AlterColumn<DateTimeOffset>(
			name: "OpenDate",
			table: "AppAdmissionOfferings",
			type: "timestamp with time zone",
			nullable: true,
			oldClrType: typeof(DateTime),
			oldType: "timestamp with time zone");
		migrationBuilder.AlterColumn<DateTimeOffset>(
			name: "CloseDate",
			table: "AppAdmissionOfferings",
			type: "timestamp with time zone",
			nullable: true,
			oldClrType: typeof(DateTime),
			oldType: "timestamp with time zone");
		migrationBuilder.AlterColumn<bool>(
			name: "IsActive",
			table: "AppAdmissionOfferings",
			type: "boolean",
			nullable: false,
			defaultValue: true,
			oldClrType: typeof(bool),
			oldType: "boolean");
		migrationBuilder.AlterColumn<bool>(
			name: "IsDeleted",
			table: "AppAdmissionOfferings",
			type: "boolean",
			nullable: false,
			defaultValue: false,
			oldClrType: typeof(bool),
			oldType: "boolean");

		migrationBuilder.CreateIndex(
			name: "UX_AdmissionOfferings_Identity_NoCampus",
			table: "AppAdmissionOfferings",
			columns: new[] { "TenantId", "AdmissionCycleId", "BoardId", "MediumId", "StageId", "StandardId" },
			unique: true,
			filter: "\"IsDeleted\" = false AND \"CampusId\" IS NULL");
		migrationBuilder.CreateIndex(
			name: "UX_AdmissionOfferings_Identity_Campus",
			table: "AppAdmissionOfferings",
			columns: new[] { "TenantId", "AdmissionCycleId", "CampusId", "BoardId", "MediumId", "StageId", "StandardId" },
			unique: true,
			filter: "\"IsDeleted\" = false AND \"CampusId\" IS NOT NULL");
		migrationBuilder.CreateIndex(
			name: "IX_AdmissionOfferings_CycleActiveStandard",
			table: "AppAdmissionOfferings",
			columns: new[] { "TenantId", "AdmissionCycleId", "IsActive", "StandardId" });
		migrationBuilder.CreateIndex(
			name: "IX_AdmissionApplications_OfferingStatus",
			table: "AppAdmissionApplications",
			columns: new[] { "TenantId", "AdmissionOfferingId", "Status" });

		migrationBuilder.AddForeignKey("FK_AdmissionOfferings_Cycles", "AppAdmissionOfferings", "AdmissionCycleId", "AppAdmissionCycles", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
		migrationBuilder.AddForeignKey("FK_AdmissionOfferings_Boards", "AppAdmissionOfferings", "BoardId", "AppEducationBoards", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
		migrationBuilder.AddForeignKey("FK_AdmissionOfferings_Mediums", "AppAdmissionOfferings", "MediumId", "AppEducationMediums", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
		migrationBuilder.AddForeignKey("FK_AdmissionOfferings_Stages", "AppAdmissionOfferings", "StageId", "AppEducationMediumStages", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
		migrationBuilder.AddForeignKey("FK_AdmissionOfferings_Standards", "AppAdmissionOfferings", "StandardId", "AppStandards", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
		migrationBuilder.AddForeignKey("FK_AdmissionOfferings_Workflows", "AppAdmissionOfferings", "WorkflowDefinitionId", "AppAdmissionWorkflowDefinitions", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
		migrationBuilder.AddForeignKey("FK_AdmissionOfferings_InquiryForms", "AppAdmissionOfferings", "InquiryFormDefinitionId", "AppAdmissionFormDefinitions", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
		migrationBuilder.AddForeignKey("FK_AdmissionOfferings_ApplicationForms", "AppAdmissionOfferings", "ApplicationFormDefinitionId", "AppAdmissionFormDefinitions", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
		migrationBuilder.AddForeignKey("FK_AdmissionApplications_Offerings", "AppAdmissionApplications", "AdmissionOfferingId", "AppAdmissionOfferings", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
		migrationBuilder.AddForeignKey("FK_AdmissionInquiries_Offerings", "AppAdmissionInquiries", "AdmissionOfferingId", "AppAdmissionOfferings", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropForeignKey("FK_AdmissionInquiries_Offerings", "AppAdmissionInquiries");
		migrationBuilder.DropForeignKey("FK_AdmissionApplications_Offerings", "AppAdmissionApplications");
		migrationBuilder.DropForeignKey("FK_AdmissionOfferings_ApplicationForms", "AppAdmissionOfferings");
		migrationBuilder.DropForeignKey("FK_AdmissionOfferings_InquiryForms", "AppAdmissionOfferings");
		migrationBuilder.DropForeignKey("FK_AdmissionOfferings_Workflows", "AppAdmissionOfferings");
		migrationBuilder.DropForeignKey("FK_AdmissionOfferings_Standards", "AppAdmissionOfferings");
		migrationBuilder.DropForeignKey("FK_AdmissionOfferings_Stages", "AppAdmissionOfferings");
		migrationBuilder.DropForeignKey("FK_AdmissionOfferings_Mediums", "AppAdmissionOfferings");
		migrationBuilder.DropForeignKey("FK_AdmissionOfferings_Boards", "AppAdmissionOfferings");
		migrationBuilder.DropForeignKey("FK_AdmissionOfferings_Cycles", "AppAdmissionOfferings");
		migrationBuilder.DropIndex("IX_AdmissionApplications_OfferingStatus", "AppAdmissionApplications");
		migrationBuilder.DropIndex("IX_AdmissionOfferings_CycleActiveStandard", "AppAdmissionOfferings");
		migrationBuilder.DropIndex("UX_AdmissionOfferings_Identity_Campus", "AppAdmissionOfferings");
		migrationBuilder.DropIndex("UX_AdmissionOfferings_Identity_NoCampus", "AppAdmissionOfferings");

		migrationBuilder.AlterColumn<DateTime>("OpenDate", "AppAdmissionOfferings", "timestamp with time zone", nullable: false, oldClrType: typeof(DateTimeOffset), oldType: "timestamp with time zone", oldNullable: true);
		migrationBuilder.AlterColumn<DateTime>("CloseDate", "AppAdmissionOfferings", "timestamp with time zone", nullable: false, oldClrType: typeof(DateTimeOffset), oldType: "timestamp with time zone", oldNullable: true);
		migrationBuilder.AlterColumn<bool>("IsActive", "AppAdmissionOfferings", "boolean", nullable: false, oldClrType: typeof(bool), oldType: "boolean", oldDefaultValue: true);
		migrationBuilder.AlterColumn<bool>("IsDeleted", "AppAdmissionOfferings", "boolean", nullable: false, oldClrType: typeof(bool), oldType: "boolean", oldDefaultValue: false);

		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionOfferings_TenantId_AdmissionCycleId_CampusId_BoardId_MediumId_StageId_StandardId",
			table: "AppAdmissionOfferings",
			columns: new[] { "TenantId", "AdmissionCycleId", "CampusId", "BoardId", "MediumId", "StageId", "StandardId" },
			unique: true);
		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionOfferings_TenantId_AdmissionCycleId_IsActive",
			table: "AppAdmissionOfferings",
			columns: new[] { "TenantId", "AdmissionCycleId", "IsActive" });
	}
}
