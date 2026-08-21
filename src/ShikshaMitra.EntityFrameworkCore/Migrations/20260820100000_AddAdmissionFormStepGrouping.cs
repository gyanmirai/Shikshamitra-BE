using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260820100000_AddAdmissionFormStepGrouping")]
public partial class AddAdmissionFormStepGrouping : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<Guid>(name: "StepId", table: "AppAdmissionFormSections", type: "uuid", nullable: true);
		migrationBuilder.AddColumn<string>(name: "StepCode", table: "AppAdmissionFormSections", type: "character varying(64)", maxLength: 64, nullable: true);
		migrationBuilder.AddColumn<string>(name: "StepName", table: "AppAdmissionFormSections", type: "character varying(128)", maxLength: 128, nullable: true);
		migrationBuilder.AddColumn<int>(name: "StepDisplayOrder", table: "AppAdmissionFormSections", type: "integer", nullable: true);
		migrationBuilder.AddColumn<string>(name: "StepVisibilityRuleJson", table: "AppAdmissionFormSections", type: "character varying(8000)", maxLength: 8000, nullable: true);

		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionFormSections_Form_Step_Order",
			table: "AppAdmissionFormSections",
			columns: new[] { "FormDefinitionId", "StepId", "StepDisplayOrder" });
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(name: "IX_AppAdmissionFormSections_Form_Step_Order", table: "AppAdmissionFormSections");
		migrationBuilder.DropColumn(name: "StepId", table: "AppAdmissionFormSections");
		migrationBuilder.DropColumn(name: "StepCode", table: "AppAdmissionFormSections");
		migrationBuilder.DropColumn(name: "StepName", table: "AppAdmissionFormSections");
		migrationBuilder.DropColumn(name: "StepDisplayOrder", table: "AppAdmissionFormSections");
		migrationBuilder.DropColumn(name: "StepVisibilityRuleJson", table: "AppAdmissionFormSections");
	}
}
