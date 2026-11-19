using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20261002010000_AddAdmissionIntakeSetups")]
public partial class AddAdmissionIntakeSetups : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "AppAdmissionIntakeSetups",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "uuid", nullable: false),
				TenantId = table.Column<Guid>(type: "uuid", nullable: true),
				Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
				InquiryIntakeProfileId = table.Column<Guid>(type: "uuid", nullable: false),
				CounsellorAssignmentProfileId = table.Column<Guid>(type: "uuid", nullable: false),
				IsArchived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
				ArchivedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
				ArchivedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
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
				table.PrimaryKey("PK_AppAdmissionIntakeSetups", item => item.Id);
				table.ForeignKey(
					name: "FK_AppAdmissionIntakeSetups_AppAdmissionIntakeProfiles_Assignment",
					column: item => item.CounsellorAssignmentProfileId,
					principalTable: "AppAdmissionIntakeProfiles",
					principalColumn: "Id",
					onDelete: ReferentialAction.Restrict);
				table.ForeignKey(
					name: "FK_AppAdmissionIntakeSetups_AppAdmissionIntakeProfiles_Inquiry",
					column: item => item.InquiryIntakeProfileId,
					principalTable: "AppAdmissionIntakeProfiles",
					principalColumn: "Id",
					onDelete: ReferentialAction.Restrict);
			});

		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionIntakeSetups_CounsellorAssignmentProfileId",
			table: "AppAdmissionIntakeSetups",
			column: "CounsellorAssignmentProfileId");
		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionIntakeSetups_InquiryIntakeProfileId",
			table: "AppAdmissionIntakeSetups",
			column: "InquiryIntakeProfileId");
		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionIntakeSetups_TenantId_IsArchived_Name",
			table: "AppAdmissionIntakeSetups",
			columns: new[] { "TenantId", "IsArchived", "Name" });
		migrationBuilder.CreateIndex(
			name: "UX_AdmissionIntakeSetups_TenantAssignmentProfile",
			table: "AppAdmissionIntakeSetups",
			columns: new[] { "TenantId", "CounsellorAssignmentProfileId" },
			unique: true,
			filter: "\"IsDeleted\" = false");
		migrationBuilder.CreateIndex(
			name: "UX_AdmissionIntakeSetups_TenantInquiryProfile",
			table: "AppAdmissionIntakeSetups",
			columns: new[] { "TenantId", "InquiryIntakeProfileId" },
			unique: true,
			filter: "\"IsDeleted\" = false");
	}

	protected override void Down(MigrationBuilder migrationBuilder) =>
		migrationBuilder.DropTable(name: "AppAdmissionIntakeSetups");
}
