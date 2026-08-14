using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable
namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260813110000_AddAdmissionWorkflows")]
public partial class AddAdmissionWorkflows : Migration
{
	protected override void Up(MigrationBuilder m)
	{
		m.CreateTable(name: "AppAdmissionWorkflowDefinitions", columns: t => new
		{
			Id = t.Column<Guid>("uuid", nullable: false), TenantId = t.Column<Guid>("uuid", nullable: true), Name = t.Column<string>("character varying(128)", maxLength: 128, nullable: false), Code = t.Column<string>("character varying(64)", maxLength: 64, nullable: false), Version = t.Column<int>("integer", nullable: false), Status = t.Column<int>("integer", nullable: false), ParentVersionId = t.Column<Guid>("uuid", nullable: true), PublishedAt = t.Column<DateTime>("timestamp with time zone", nullable: true), PublishedBy = t.Column<Guid>("uuid", nullable: true), ExtraProperties = t.Column<string>("text", nullable: false), ConcurrencyStamp = t.Column<string>("character varying(40)", maxLength: 40, nullable: false), CreationTime = t.Column<DateTime>("timestamp with time zone", nullable: false), CreatorId = t.Column<Guid>("uuid", nullable: true), LastModificationTime = t.Column<DateTime>("timestamp with time zone", nullable: true), LastModifierId = t.Column<Guid>("uuid", nullable: true), IsDeleted = t.Column<bool>("boolean", nullable: false), DeleterId = t.Column<Guid>("uuid", nullable: true), DeletionTime = t.Column<DateTime>("timestamp with time zone", nullable: true)
		}, constraints: t => t.PrimaryKey("PK_AppAdmissionWorkflowDefinitions", x => x.Id));
		m.CreateTable(name: "AppAdmissionWorkflowStages", columns: t => new { Id = t.Column<Guid>("uuid", nullable: false), WorkflowDefinitionId = t.Column<Guid>("uuid", nullable: false), Code = t.Column<string>("character varying(64)", maxLength: 64, nullable: false), Name = t.Column<string>("character varying(128)", maxLength: 128, nullable: false), Description = t.Column<string>("character varying(512)", maxLength: 512, nullable: true), StageType = t.Column<int>("integer", nullable: false), DisplayOrder = t.Column<int>("integer", nullable: false), IsRequired = t.Column<bool>("boolean", nullable: false), AllowManualSkip = t.Column<bool>("boolean", nullable: false), ConfigurationJson = t.Column<string>("character varying(8000)", maxLength: 8000, nullable: true) }, constraints: t => t.PrimaryKey("PK_AppAdmissionWorkflowStages", x => x.Id));
		m.CreateTable(name: "AppAdmissionWorkflowTransitions", columns: t => new { Id = t.Column<Guid>("uuid", nullable: false), WorkflowDefinitionId = t.Column<Guid>("uuid", nullable: false), FromStageId = t.Column<Guid>("uuid", nullable: false), ToStageId = t.Column<Guid>("uuid", nullable: false), Priority = t.Column<int>("integer", nullable: false) }, constraints: t => t.PrimaryKey("PK_AppAdmissionWorkflowTransitions", x => x.Id));
		m.CreateIndex("IX_AppAdmissionWorkflowDefinitions_TenantId_Code_Version", "AppAdmissionWorkflowDefinitions", new[] { "TenantId", "Code", "Version" }, unique: true);
		m.CreateIndex("IX_AppAdmissionWorkflowDefinitions_TenantId_Code_Status", "AppAdmissionWorkflowDefinitions", new[] { "TenantId", "Code", "Status" });
		m.CreateIndex("IX_AppAdmissionWorkflowStages_WorkflowDefinitionId_Code", "AppAdmissionWorkflowStages", new[] { "WorkflowDefinitionId", "Code" }, unique: true);
		m.CreateIndex("IX_AppAdmissionWorkflowTransitions_WorkflowDefinitionId_FromStageId_ToStageId", "AppAdmissionWorkflowTransitions", new[] { "WorkflowDefinitionId", "FromStageId", "ToStageId" }, unique: true);
	}
	protected override void Down(MigrationBuilder m) { m.DropTable("AppAdmissionWorkflowTransitions"); m.DropTable("AppAdmissionWorkflowStages"); m.DropTable("AppAdmissionWorkflowDefinitions"); }
}
