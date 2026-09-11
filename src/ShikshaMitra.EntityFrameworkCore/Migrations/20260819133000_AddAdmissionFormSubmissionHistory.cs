using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260819133000_AddAdmissionFormSubmissionHistory")]
public partial class AddAdmissionFormSubmissionHistory : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<int>("Status", "AppAdmissionFormInstances", type: "integer", nullable: false, defaultValue: 0);
		migrationBuilder.AddColumn<DateTime>("LastSavedAt", "AppAdmissionFormInstances", type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP");
		migrationBuilder.AddColumn<DateTime>("SubmittedAt", "AppAdmissionFormInstances", type: "timestamp with time zone", nullable: true);
		migrationBuilder.AddColumn<Guid>("SubmittedBy", "AppAdmissionFormInstances", type: "uuid", nullable: true);

		migrationBuilder.CreateTable(
			name: "AppAdmissionFormSubmissions",
			columns: table => new
			{
				Id = table.Column<Guid>("uuid", nullable: false),
				TenantId = table.Column<Guid>("uuid", nullable: true),
				FormInstanceId = table.Column<Guid>("uuid", nullable: false),
				ApplicationId = table.Column<Guid>("uuid", nullable: false),
				AttemptNumber = table.Column<int>("integer", nullable: false),
				SubmittedAt = table.Column<DateTime>("timestamp with time zone", nullable: false),
				SubmittedBy = table.Column<Guid>("uuid", nullable: true),
				SnapshotJson = table.Column<string>("text", nullable: false),
				ExtraProperties = table.Column<string>("text", nullable: false),
				ConcurrencyStamp = table.Column<string>("character varying(40)", maxLength: 40, nullable: false),
				CreationTime = table.Column<DateTime>("timestamp with time zone", nullable: false),
				CreatorId = table.Column<Guid>("uuid", nullable: true),
				LastModificationTime = table.Column<DateTime>("timestamp with time zone", nullable: true),
				LastModifierId = table.Column<Guid>("uuid", nullable: true),
				IsDeleted = table.Column<bool>("boolean", nullable: false, defaultValue: false),
				DeleterId = table.Column<Guid>("uuid", nullable: true),
				DeletionTime = table.Column<DateTime>("timestamp with time zone", nullable: true)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_AppAdmissionFormSubmissions", x => x.Id);
				table.ForeignKey("FK_AdmissionFormSubmissions_Instances", x => x.FormInstanceId, "AppAdmissionFormInstances", "Id", onDelete: ReferentialAction.Restrict);
				table.ForeignKey("FK_AdmissionFormSubmissions_Applications", x => x.ApplicationId, "AppAdmissionApplications", "Id", onDelete: ReferentialAction.Restrict);
			});

		migrationBuilder.CreateTable(
			name: "AppAdmissionFormChangeHistories",
			columns: table => new
			{
				Id = table.Column<Guid>("uuid", nullable: false),
				TenantId = table.Column<Guid>("uuid", nullable: true),
				FormInstanceId = table.Column<Guid>("uuid", nullable: false),
				ApplicationId = table.Column<Guid>("uuid", nullable: false),
				ChangeType = table.Column<string>("character varying(64)", maxLength: 64, nullable: false),
				ChangedFieldsJson = table.Column<string>("text", nullable: true),
				ChangedBy = table.Column<Guid>("uuid", nullable: true),
				OccurredAt = table.Column<DateTime>("timestamp with time zone", nullable: false),
				ExtraProperties = table.Column<string>("text", nullable: false),
				ConcurrencyStamp = table.Column<string>("character varying(40)", maxLength: 40, nullable: false),
				CreationTime = table.Column<DateTime>("timestamp with time zone", nullable: false),
				CreatorId = table.Column<Guid>("uuid", nullable: true),
				LastModificationTime = table.Column<DateTime>("timestamp with time zone", nullable: true),
				LastModifierId = table.Column<Guid>("uuid", nullable: true),
				IsDeleted = table.Column<bool>("boolean", nullable: false, defaultValue: false),
				DeleterId = table.Column<Guid>("uuid", nullable: true),
				DeletionTime = table.Column<DateTime>("timestamp with time zone", nullable: true)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_AppAdmissionFormChangeHistories", x => x.Id);
				table.ForeignKey("FK_AdmissionFormChangeHistories_Instances", x => x.FormInstanceId, "AppAdmissionFormInstances", "Id", onDelete: ReferentialAction.Cascade);
				table.ForeignKey("FK_AdmissionFormChangeHistories_Applications", x => x.ApplicationId, "AppAdmissionApplications", "Id", onDelete: ReferentialAction.Restrict);
			});

		migrationBuilder.CreateIndex("IX_AppAdmissionFormInstances_TenantId_Status", "AppAdmissionFormInstances", new[] { "TenantId", "Status" });
		migrationBuilder.CreateIndex("IX_AppAdmissionFormSubmissions_TenantId_FormInstanceId_AttemptNumber", "AppAdmissionFormSubmissions", new[] { "TenantId", "FormInstanceId", "AttemptNumber" }, unique: true, filter: "\"IsDeleted\" = false");
		migrationBuilder.CreateIndex("IX_AppAdmissionFormSubmissions_TenantId_ApplicationId_SubmittedAt", "AppAdmissionFormSubmissions", new[] { "TenantId", "ApplicationId", "SubmittedAt" });
		migrationBuilder.CreateIndex("IX_AppAdmissionFormChangeHistories_TenantId_FormInstanceId_OccurredAt", "AppAdmissionFormChangeHistories", new[] { "TenantId", "FormInstanceId", "OccurredAt" });
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable("AppAdmissionFormSubmissions");
		migrationBuilder.DropTable("AppAdmissionFormChangeHistories");
		migrationBuilder.DropIndex("IX_AppAdmissionFormInstances_TenantId_Status", "AppAdmissionFormInstances");
		migrationBuilder.DropColumn("Status", "AppAdmissionFormInstances");
		migrationBuilder.DropColumn("LastSavedAt", "AppAdmissionFormInstances");
		migrationBuilder.DropColumn("SubmittedAt", "AppAdmissionFormInstances");
		migrationBuilder.DropColumn("SubmittedBy", "AppAdmissionFormInstances");
	}
}
