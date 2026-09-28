using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260926090000_AddWorkflowDefaultInquiryForm")]
public partial class AddWorkflowDefaultInquiryForm : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<Guid>(
			name: "DefaultInquiryFormDefinitionId",
			table: "AppAdmissionWorkflowDefinitions",
			type: "uuid",
			nullable: true);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(
			name: "DefaultInquiryFormDefinitionId",
			table: "AppAdmissionWorkflowDefinitions");
	}
}
