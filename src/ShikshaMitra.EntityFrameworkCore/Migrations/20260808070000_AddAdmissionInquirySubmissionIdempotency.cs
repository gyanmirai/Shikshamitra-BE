using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260808070000_AddAdmissionInquirySubmissionIdempotency")]
public partial class AddAdmissionInquirySubmissionIdempotency : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<string>(
			name: "SubmissionKey",
			table: "AppAdmissionInquiryFormSubmissions",
			type: "character varying(64)",
			maxLength: 64,
			nullable: true);

		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionInquiryFormSubmissions_TenantId_FormTemplateId_SubmissionKey",
			table: "AppAdmissionInquiryFormSubmissions",
			columns: new[] { "TenantId", "FormTemplateId", "SubmissionKey" },
			unique: true);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(
			name: "IX_AppAdmissionInquiryFormSubmissions_TenantId_FormTemplateId_SubmissionKey",
			table: "AppAdmissionInquiryFormSubmissions");

		migrationBuilder.DropColumn(
			name: "SubmissionKey",
			table: "AppAdmissionInquiryFormSubmissions");
	}
}
