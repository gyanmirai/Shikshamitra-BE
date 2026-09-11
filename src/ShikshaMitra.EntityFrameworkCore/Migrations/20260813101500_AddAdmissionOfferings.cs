using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260813101500_AddAdmissionOfferings")]
public partial class AddAdmissionOfferings : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "AppAdmissionOfferings",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "uuid", nullable: false),
				TenantId = table.Column<Guid>(type: "uuid", nullable: true),
				AdmissionCycleId = table.Column<Guid>(type: "uuid", nullable: false),
				CampusId = table.Column<Guid>(type: "uuid", nullable: true),
				BoardId = table.Column<Guid>(type: "uuid", nullable: false),
				MediumId = table.Column<Guid>(type: "uuid", nullable: false),
				StageId = table.Column<Guid>(type: "uuid", nullable: false),
				StandardId = table.Column<Guid>(type: "uuid", nullable: false),
				Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
				OpenDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
				CloseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
				IsActive = table.Column<bool>(type: "boolean", nullable: false),
				WorkflowDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
				InquiryFormDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
				ApplicationFormDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
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
			constraints: table => table.PrimaryKey("PK_AppAdmissionOfferings", x => x.Id));

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

	protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "AppAdmissionOfferings");
}
