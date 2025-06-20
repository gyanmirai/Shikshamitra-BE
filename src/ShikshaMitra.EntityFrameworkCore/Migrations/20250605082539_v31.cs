using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v31 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TemporaryGr",
                table: "AppStudentEnrollments");

            migrationBuilder.RenameColumn(
                name: "MotherDetails_OfficeAddress",
                table: "AppStudents",
                newName: "MotherDetails_OfficeAddress3");

            migrationBuilder.RenameColumn(
                name: "MotherDetails_Address",
                table: "AppStudents",
                newName: "MotherDetails_OfficeAddress2");

            migrationBuilder.RenameColumn(
                name: "GuardianDetails_OfficeAddress",
                table: "AppStudents",
                newName: "MotherDetails_OfficeAddress1");

            migrationBuilder.RenameColumn(
                name: "GuardianDetails_Address",
                table: "AppStudents",
                newName: "MotherDetails_Address3");

            migrationBuilder.RenameColumn(
                name: "FatherDetails_OfficeAddress",
                table: "AppStudents",
                newName: "MotherDetails_Address2");

            migrationBuilder.RenameColumn(
                name: "FatherDetails_Address",
                table: "AppStudents",
                newName: "MotherDetails_Address1");

            migrationBuilder.RenameColumn(
                name: "Village",
                table: "AppStudentEnrollments",
                newName: "SecondaryContactNumber");

            migrationBuilder.RenameColumn(
                name: "ParentContactNumber",
                table: "AppStudentEnrollments",
                newName: "PrimaryContactNumber");

            migrationBuilder.RenameColumn(
                name: "MotherDetails_OfficeAddress",
                table: "AppStudentEnrollments",
                newName: "MotherDetails_OfficeAddress3");

            migrationBuilder.RenameColumn(
                name: "MotherDetails_Address",
                table: "AppStudentEnrollments",
                newName: "MotherDetails_OfficeAddress2");

            migrationBuilder.RenameColumn(
                name: "GuardianDetails_OfficeAddress",
                table: "AppStudentEnrollments",
                newName: "MotherDetails_OfficeAddress1");

            migrationBuilder.RenameColumn(
                name: "GuardianDetails_Address",
                table: "AppStudentEnrollments",
                newName: "MotherDetails_Address3");

            migrationBuilder.RenameColumn(
                name: "FatherDetails_OfficeAddress",
                table: "AppStudentEnrollments",
                newName: "MotherDetails_Address2");

            migrationBuilder.RenameColumn(
                name: "FatherDetails_Address",
                table: "AppStudentEnrollments",
                newName: "MotherDetails_Address1");

            migrationBuilder.RenameColumn(
                name: "AlternativeContactNumber",
                table: "AppStudentEnrollments",
                newName: "GuardianDetails_OfficeAddress3");

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_Address1",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_Address2",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_Address3",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_OfficeAddress1",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_OfficeAddress2",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_OfficeAddress3",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_Address1",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_Address2",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_Address3",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_OfficeAddress1",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_OfficeAddress2",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_OfficeAddress3",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_Address1",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_Address2",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_Address3",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_OfficeAddress1",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_OfficeAddress2",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_OfficeAddress3",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_Address1",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_Address2",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_Address3",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_OfficeAddress1",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_OfficeAddress2",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FatherDetails_Address1",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_Address2",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_Address3",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_OfficeAddress1",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_OfficeAddress2",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_OfficeAddress3",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_Address1",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_Address2",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_Address3",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_OfficeAddress1",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_OfficeAddress2",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_OfficeAddress3",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_Address1",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "FatherDetails_Address2",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "FatherDetails_Address3",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "FatherDetails_OfficeAddress1",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "FatherDetails_OfficeAddress2",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "FatherDetails_OfficeAddress3",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_Address1",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_Address2",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_Address3",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_OfficeAddress1",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_OfficeAddress2",
                table: "AppStudentEnrollments");

            migrationBuilder.RenameColumn(
                name: "MotherDetails_OfficeAddress3",
                table: "AppStudents",
                newName: "MotherDetails_OfficeAddress");

            migrationBuilder.RenameColumn(
                name: "MotherDetails_OfficeAddress2",
                table: "AppStudents",
                newName: "MotherDetails_Address");

            migrationBuilder.RenameColumn(
                name: "MotherDetails_OfficeAddress1",
                table: "AppStudents",
                newName: "GuardianDetails_OfficeAddress");

            migrationBuilder.RenameColumn(
                name: "MotherDetails_Address3",
                table: "AppStudents",
                newName: "GuardianDetails_Address");

            migrationBuilder.RenameColumn(
                name: "MotherDetails_Address2",
                table: "AppStudents",
                newName: "FatherDetails_OfficeAddress");

            migrationBuilder.RenameColumn(
                name: "MotherDetails_Address1",
                table: "AppStudents",
                newName: "FatherDetails_Address");

            migrationBuilder.RenameColumn(
                name: "SecondaryContactNumber",
                table: "AppStudentEnrollments",
                newName: "Village");

            migrationBuilder.RenameColumn(
                name: "PrimaryContactNumber",
                table: "AppStudentEnrollments",
                newName: "ParentContactNumber");

            migrationBuilder.RenameColumn(
                name: "MotherDetails_OfficeAddress3",
                table: "AppStudentEnrollments",
                newName: "MotherDetails_OfficeAddress");

            migrationBuilder.RenameColumn(
                name: "MotherDetails_OfficeAddress2",
                table: "AppStudentEnrollments",
                newName: "MotherDetails_Address");

            migrationBuilder.RenameColumn(
                name: "MotherDetails_OfficeAddress1",
                table: "AppStudentEnrollments",
                newName: "GuardianDetails_OfficeAddress");

            migrationBuilder.RenameColumn(
                name: "MotherDetails_Address3",
                table: "AppStudentEnrollments",
                newName: "GuardianDetails_Address");

            migrationBuilder.RenameColumn(
                name: "MotherDetails_Address2",
                table: "AppStudentEnrollments",
                newName: "FatherDetails_OfficeAddress");

            migrationBuilder.RenameColumn(
                name: "MotherDetails_Address1",
                table: "AppStudentEnrollments",
                newName: "FatherDetails_Address");

            migrationBuilder.RenameColumn(
                name: "GuardianDetails_OfficeAddress3",
                table: "AppStudentEnrollments",
                newName: "AlternativeContactNumber");

            migrationBuilder.AddColumn<int>(
                name: "TemporaryGr",
                table: "AppStudentEnrollments",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
