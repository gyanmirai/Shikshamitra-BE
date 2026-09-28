using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260925100000_AddAdmissionApplicationCounsellor")]
public partial class AddAdmissionApplicationCounsellor : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<Guid>(
			name: "AssignedCounsellorId",
			table: "AppAdmissionApplications",
			type: "uuid",
			nullable: true);

		migrationBuilder.Sql("""
			UPDATE "AppAdmissionApplications" AS application
			SET "AssignedCounsellorId" = inquiry."AssignedCounsellorId"
			FROM "AppAdmissionInquiries" AS inquiry
			WHERE application."InquiryId" = inquiry."Id"
			  AND application."TenantId" IS NOT DISTINCT FROM inquiry."TenantId"
			  AND application."IsDeleted" = false
			  AND inquiry."IsDeleted" = false
			  AND inquiry."AssignedCounsellorId" IS NOT NULL;
			""");

		migrationBuilder.CreateIndex(
			name: "IX_AdmissionApplications_AssignedCounsellor",
			table: "AppAdmissionApplications",
			columns: new[] { "TenantId", "AssignedCounsellorId" });
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(
			name: "IX_AdmissionApplications_AssignedCounsellor",
			table: "AppAdmissionApplications");
		migrationBuilder.DropColumn(
			name: "AssignedCounsellorId",
			table: "AppAdmissionApplications");
	}
}
