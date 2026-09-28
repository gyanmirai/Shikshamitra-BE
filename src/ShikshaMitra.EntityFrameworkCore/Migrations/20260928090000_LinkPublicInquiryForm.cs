using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260928090000_LinkPublicInquiryForm")]
public partial class LinkPublicInquiryForm : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<Guid>(
			name: "InquiryFormDefinitionId",
			table: "AppAdmissionPublicInquiryLinks",
			type: "uuid",
			nullable: true);
		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionPublicInquiryLinks_InquiryFormDefinitionId",
			table: "AppAdmissionPublicInquiryLinks",
			column: "InquiryFormDefinitionId");
		migrationBuilder.AddForeignKey(
			name: "FK_PublicInquiryLinks_InquiryForm",
			table: "AppAdmissionPublicInquiryLinks",
			column: "InquiryFormDefinitionId",
			principalTable: "AppAdmissionFormDefinitions",
			principalColumn: "Id",
			onDelete: ReferentialAction.Restrict);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropForeignKey(
			name: "FK_PublicInquiryLinks_InquiryForm",
			table: "AppAdmissionPublicInquiryLinks");
		migrationBuilder.DropIndex(
			name: "IX_AppAdmissionPublicInquiryLinks_InquiryFormDefinitionId",
			table: "AppAdmissionPublicInquiryLinks");
		migrationBuilder.DropColumn(
			name: "InquiryFormDefinitionId",
			table: "AppAdmissionPublicInquiryLinks");
	}
}
