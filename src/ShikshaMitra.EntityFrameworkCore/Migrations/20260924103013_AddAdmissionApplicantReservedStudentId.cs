using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionApplicantReservedStudentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReservedStudentId",
                table: "AppAdmissionApplicants",
                type: "uuid",
                nullable: true);

            // Include deleted and already-linked applicants. Historical Student IDs and links stay
            // unchanged; reservations on already-linked applicants are intentionally unused.
            migrationBuilder.Sql("""
                UPDATE "AppAdmissionApplicants"
                SET "ReservedStudentId" = gen_random_uuid()
                WHERE "ReservedStudentId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "ReservedStudentId",
                table: "AppAdmissionApplicants",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_AdmissionApplicants_ReservedStudentId",
                table: "AppAdmissionApplicants",
                column: "ReservedStudentId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AdmissionApplicants_ReservedStudentId",
                table: "AppAdmissionApplicants",
                sql: "\"ReservedStudentId\" <> '00000000-0000-0000-0000-000000000000'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_AdmissionApplicants_ReservedStudentId",
                table: "AppAdmissionApplicants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AdmissionApplicants_ReservedStudentId",
                table: "AppAdmissionApplicants");

            migrationBuilder.DropColumn(
                name: "ReservedStudentId",
                table: "AppAdmissionApplicants");
        }
    }
}
