using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260819143000_AddAdmissionApplicantAddressFields")]
public partial class AddAdmissionApplicantAddressFields : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<string>(name: "AddressLine1", table: "AppAdmissionApplicants", type: "character varying(256)", maxLength: 256, nullable: true);
		migrationBuilder.AddColumn<string>(name: "AddressLine2", table: "AppAdmissionApplicants", type: "character varying(256)", maxLength: 256, nullable: true);
		migrationBuilder.AddColumn<string>(name: "City", table: "AppAdmissionApplicants", type: "character varying(128)", maxLength: 128, nullable: true);
		migrationBuilder.AddColumn<string>(name: "Country", table: "AppAdmissionApplicants", type: "character varying(128)", maxLength: 128, nullable: true);
		migrationBuilder.AddColumn<string>(name: "PostalCode", table: "AppAdmissionApplicants", type: "character varying(32)", maxLength: 32, nullable: true);
		migrationBuilder.AddColumn<string>(name: "State", table: "AppAdmissionApplicants", type: "character varying(128)", maxLength: 128, nullable: true);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(name: "AddressLine1", table: "AppAdmissionApplicants");
		migrationBuilder.DropColumn(name: "AddressLine2", table: "AppAdmissionApplicants");
		migrationBuilder.DropColumn(name: "City", table: "AppAdmissionApplicants");
		migrationBuilder.DropColumn(name: "Country", table: "AppAdmissionApplicants");
		migrationBuilder.DropColumn(name: "PostalCode", table: "AppAdmissionApplicants");
		migrationBuilder.DropColumn(name: "State", table: "AppAdmissionApplicants");
	}
}
