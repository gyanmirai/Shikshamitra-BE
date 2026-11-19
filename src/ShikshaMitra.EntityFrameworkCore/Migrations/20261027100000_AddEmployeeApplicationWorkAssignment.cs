using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20261027100000_AddEmployeeApplicationWorkAssignment")]
public class AddEmployeeApplicationWorkAssignment : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<string>(name: "WorkAssignmentMode", table: "AppEmployeeApplications",
			type: "character varying(32)", maxLength: 32, nullable: true);
		migrationBuilder.AddColumn<Guid>(name: "WorkAssignmentAcademicYearId", table: "AppEmployeeApplications",
			type: "uuid", nullable: true);
		migrationBuilder.AddColumn<string>(name: "WorkAssignmentAcademicYearName", table: "AppEmployeeApplications",
			type: "character varying(256)", maxLength: 256, nullable: true);
		migrationBuilder.AddColumn<Guid[]>(name: "WorkAssignmentOrganizationIds", table: "AppEmployeeApplications",
			type: "uuid[]", nullable: false, defaultValue: Array.Empty<Guid>());
		migrationBuilder.AddColumn<string[]>(name: "WorkAssignmentOrganizationLabels", table: "AppEmployeeApplications",
			type: "text[]", nullable: false, defaultValue: Array.Empty<string>());
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(name: "WorkAssignmentMode", table: "AppEmployeeApplications");
		migrationBuilder.DropColumn(name: "WorkAssignmentAcademicYearId", table: "AppEmployeeApplications");
		migrationBuilder.DropColumn(name: "WorkAssignmentAcademicYearName", table: "AppEmployeeApplications");
		migrationBuilder.DropColumn(name: "WorkAssignmentOrganizationIds", table: "AppEmployeeApplications");
		migrationBuilder.DropColumn(name: "WorkAssignmentOrganizationLabels", table: "AppEmployeeApplications");
	}
}
