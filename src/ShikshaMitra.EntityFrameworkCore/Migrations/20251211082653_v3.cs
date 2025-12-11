using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Country",
                table: "AppStudentContactDetails");

            migrationBuilder.DropColumn(
                name: "State",
                table: "AppStudentContactDetails");

            migrationBuilder.AddColumn<Guid>(
                name: "InquiryPreferenceId",
                table: "AppStudents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SecondaryContactNo",
                table: "AppStudentContactDetails",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PrimaryContactNo",
                table: "AppStudentContactDetails",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Pincode",
                table: "AppStudentContactDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "AppStudentContactDetails",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "AppStudentContactDetails",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address3",
                table: "AppStudentContactDetails",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address2",
                table: "AppStudentContactDetails",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address1",
                table: "AppStudentContactDetails",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country_IsoCode",
                table: "AppStudentContactDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country_Name",
                table: "AppStudentContactDetails",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State_IsoCode",
                table: "AppStudentContactDetails",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State_Name",
                table: "AppStudentContactDetails",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_EducationMediumId",
                table: "AppStudents",
                column: "EducationMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_InquiryPreferenceId",
                table: "AppStudents",
                column: "InquiryPreferenceId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppEducationMediums_EducationMediumId",
                table: "AppStudents",
                column: "EducationMediumId",
                principalTable: "AppEducationMediums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppInquiryPreferences_InquiryPreferenceId",
                table: "AppStudents",
                column: "InquiryPreferenceId",
                principalTable: "AppInquiryPreferences",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppEducationMediums_EducationMediumId",
                table: "AppStudents");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppInquiryPreferences_InquiryPreferenceId",
                table: "AppStudents");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_EducationMediumId",
                table: "AppStudents");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_InquiryPreferenceId",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "InquiryPreferenceId",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "Country_IsoCode",
                table: "AppStudentContactDetails");

            migrationBuilder.DropColumn(
                name: "Country_Name",
                table: "AppStudentContactDetails");

            migrationBuilder.DropColumn(
                name: "State_IsoCode",
                table: "AppStudentContactDetails");

            migrationBuilder.DropColumn(
                name: "State_Name",
                table: "AppStudentContactDetails");

            migrationBuilder.AlterColumn<string>(
                name: "SecondaryContactNo",
                table: "AppStudentContactDetails",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PrimaryContactNo",
                table: "AppStudentContactDetails",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Pincode",
                table: "AppStudentContactDetails",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "AppStudentContactDetails",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "AppStudentContactDetails",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address3",
                table: "AppStudentContactDetails",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address2",
                table: "AppStudentContactDetails",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address1",
                table: "AppStudentContactDetails",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "AppStudentContactDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "AppStudentContactDetails",
                type: "text",
                nullable: true);
        }
    }
}
