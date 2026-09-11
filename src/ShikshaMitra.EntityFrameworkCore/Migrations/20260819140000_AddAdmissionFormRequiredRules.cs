using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260819140000_AddAdmissionFormRequiredRules")]
public partial class AddAdmissionFormRequiredRules : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<string>(
			name: "RequiredRuleJson",
			table: "AppAdmissionFormFields",
			type: "character varying(8000)",
			maxLength: 8000,
			nullable: true);
	}

	protected override void Down(MigrationBuilder migrationBuilder) =>
		migrationBuilder.DropColumn(name: "RequiredRuleJson", table: "AppAdmissionFormFields");
}
