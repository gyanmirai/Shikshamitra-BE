using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260813103000_AddAdmissionInquiries")]
public partial class AddAdmissionInquiries : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(name: "AppAdmissionInquiries", columns: table => new
		{
			Id = table.Column<Guid>(type: "uuid", nullable: false),
			TenantId = table.Column<Guid>(type: "uuid", nullable: true),
			InquiryNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
			AdmissionCycleId = table.Column<Guid>(type: "uuid", nullable: false),
			AdmissionOfferingId = table.Column<Guid>(type: "uuid", nullable: true),
			FamilyId = table.Column<Guid>(type: "uuid", nullable: true),
			ApplicantId = table.Column<Guid>(type: "uuid", nullable: true),
			AssignedCounsellorId = table.Column<Guid>(type: "uuid", nullable: true),
			ApplicantName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
			NormalizedPhone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
			Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
			Source = table.Column<int>(type: "integer", nullable: false),
			SourceDetail = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
			Status = table.Column<int>(type: "integer", nullable: false),
			DuplicateOfId = table.Column<Guid>(type: "uuid", nullable: true),
			ContactedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
			LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
			ExtraProperties = table.Column<string>(type: "text", nullable: false),
			ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
			CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
			CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
			LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
			LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
			IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
			DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
			DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
		}, constraints: table => table.PrimaryKey("PK_AppAdmissionInquiries", x => x.Id));
		migrationBuilder.CreateIndex(name: "IX_AppAdmissionInquiries_TenantId_AdmissionCycleId_NormalizedPhone", table: "AppAdmissionInquiries", columns: new[] { "TenantId", "AdmissionCycleId", "NormalizedPhone" });
		migrationBuilder.CreateIndex(name: "IX_AppAdmissionInquiries_TenantId_InquiryNumber", table: "AppAdmissionInquiries", columns: new[] { "TenantId", "InquiryNumber" }, unique: true);
			migrationBuilder.CreateIndex(name: "IX_AppAdmissionInquiries_TenantId_AdmissionCycleId_Status", table: "AppAdmissionInquiries", columns: new[] { "TenantId", "AdmissionCycleId", "Status" });
		migrationBuilder.CreateIndex(name: "IX_AppAdmissionInquiries_TenantId_AssignedCounsellorId", table: "AppAdmissionInquiries", columns: new[] { "TenantId", "AssignedCounsellorId" });
	}

	protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "AppAdmissionInquiries");
}
