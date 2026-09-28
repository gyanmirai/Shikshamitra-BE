using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260924100000_AddAdmissionOfferingTracks")]
public partial class AddAdmissionOfferingTracks : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex("UX_AdmissionOfferings_Identity_NoCampus", "AppAdmissionOfferings");
		migrationBuilder.DropIndex("UX_AdmissionOfferings_Identity_Campus", "AppAdmissionOfferings");
		migrationBuilder.AddColumn<int>("Track", "AppAdmissionOfferings", type: "integer", nullable: false, defaultValue: 0);
		migrationBuilder.AddColumn<int>("EntryMode", "AppAdmissionOfferings", type: "integer", nullable: false, defaultValue: 0);
		migrationBuilder.CreateIndex(
			"UX_AdmissionOfferings_Identity_NoCampus", "AppAdmissionOfferings",
			new[] { "TenantId", "AdmissionCycleId", "BoardId", "MediumId", "StageId", "StandardId", "Track" },
			unique: true, filter: "\"IsDeleted\" = false AND \"CampusId\" IS NULL");
		migrationBuilder.CreateIndex(
			"UX_AdmissionOfferings_Identity_Campus", "AppAdmissionOfferings",
			new[] { "TenantId", "AdmissionCycleId", "CampusId", "BoardId", "MediumId", "StageId", "StandardId", "Track" },
			unique: true, filter: "\"IsDeleted\" = false AND \"CampusId\" IS NOT NULL");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex("UX_AdmissionOfferings_Identity_NoCampus", "AppAdmissionOfferings");
		migrationBuilder.DropIndex("UX_AdmissionOfferings_Identity_Campus", "AppAdmissionOfferings");
		migrationBuilder.DropColumn("Track", "AppAdmissionOfferings");
		migrationBuilder.DropColumn("EntryMode", "AppAdmissionOfferings");
		migrationBuilder.CreateIndex(
			"UX_AdmissionOfferings_Identity_NoCampus", "AppAdmissionOfferings",
			new[] { "TenantId", "AdmissionCycleId", "BoardId", "MediumId", "StageId", "StandardId" },
			unique: true, filter: "\"IsDeleted\" = false AND \"CampusId\" IS NULL");
		migrationBuilder.CreateIndex(
			"UX_AdmissionOfferings_Identity_Campus", "AppAdmissionOfferings",
			new[] { "TenantId", "AdmissionCycleId", "CampusId", "BoardId", "MediumId", "StageId", "StandardId" },
			unique: true, filter: "\"IsDeleted\" = false AND \"CampusId\" IS NOT NULL");
	}
}
