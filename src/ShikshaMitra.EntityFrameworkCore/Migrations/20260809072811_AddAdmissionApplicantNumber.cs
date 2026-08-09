using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionApplicantNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApplicantNumber",
                table: "AppAdmissionInquiryLeads",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH numbered AS (
                    SELECT "Id", row_number() OVER (ORDER BY "CreationTime", "Id")::integer AS applicant_number
                    FROM "AppAdmissionInquiryLeads"
                )
                UPDATE "AppAdmissionInquiryLeads" AS leads
                SET "ApplicantNumber" = numbered.applicant_number
                FROM numbered
                WHERE leads."Id" = numbered."Id";

                ALTER TABLE "AppAdmissionInquiryLeads"
                    ALTER COLUMN "ApplicantNumber" SET NOT NULL,
                    ALTER COLUMN "ApplicantNumber" ADD GENERATED ALWAYS AS IDENTITY;

                SELECT setval(
                    pg_get_serial_sequence('"AppAdmissionInquiryLeads"', 'ApplicantNumber'),
                    (SELECT COALESCE(MAX("ApplicantNumber"), 0) + 1 FROM "AppAdmissionInquiryLeads"),
                    false);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionInquiryLeads_Tenant_ApplicantNumber",
                table: "AppAdmissionInquiryLeads",
                columns: new[] { "TenantId", "ApplicantNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AdmissionInquiryLeads_Tenant_ApplicantNumber",
                table: "AppAdmissionInquiryLeads");

            migrationBuilder.DropColumn(
                name: "ApplicantNumber",
                table: "AppAdmissionInquiryLeads");
        }
    }
}
