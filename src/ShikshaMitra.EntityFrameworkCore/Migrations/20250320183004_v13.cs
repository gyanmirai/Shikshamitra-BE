using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StudentCommunicationDetails_PermanentState",
                table: "AppStudents",
                newName: "StudentCommunicationDetails_PermanentStateName");

            migrationBuilder.RenameColumn(
                name: "StudentCommunicationDetails_PermanentCountry",
                table: "AppStudents",
                newName: "StudentCommunicationDetails_PermanentStateIsoCode");

            migrationBuilder.RenameColumn(
                name: "StudentCommunicationDetails_LocalState",
                table: "AppStudents",
                newName: "StudentCommunicationDetails_PermanentCountryName");

            migrationBuilder.RenameColumn(
                name: "StudentCommunicationDetails_LocalCountry",
                table: "AppStudents",
                newName: "StudentCommunicationDetails_PermanentCountryIsoCode");

            migrationBuilder.RenameColumn(
                name: "StudentCommunicationDetails_PermanentState",
                table: "AppStudentEnrollments",
                newName: "StudentCommunicationDetails_PermanentStateName");

            migrationBuilder.RenameColumn(
                name: "StudentCommunicationDetails_PermanentCountry",
                table: "AppStudentEnrollments",
                newName: "StudentCommunicationDetails_PermanentStateIsoCode");

            migrationBuilder.RenameColumn(
                name: "StudentCommunicationDetails_LocalState",
                table: "AppStudentEnrollments",
                newName: "StudentCommunicationDetails_PermanentCountryName");

            migrationBuilder.RenameColumn(
                name: "StudentCommunicationDetails_LocalCountry",
                table: "AppStudentEnrollments",
                newName: "StudentCommunicationDetails_PermanentCountryIsoCode");

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_LocalCountryIsoCode",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_LocalCountryName",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_LocalStateIsoCode",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_LocalStateName",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_LocalCountryIsoCode",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_LocalCountryName",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_LocalStateIsoCode",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_LocalStateName",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_LocalCountryIsoCode",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_LocalCountryName",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_LocalStateIsoCode",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_LocalStateName",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_LocalCountryIsoCode",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_LocalCountryName",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_LocalStateIsoCode",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_LocalStateName",
                table: "AppStudentEnrollments");

            migrationBuilder.RenameColumn(
                name: "StudentCommunicationDetails_PermanentStateName",
                table: "AppStudents",
                newName: "StudentCommunicationDetails_PermanentState");

            migrationBuilder.RenameColumn(
                name: "StudentCommunicationDetails_PermanentStateIsoCode",
                table: "AppStudents",
                newName: "StudentCommunicationDetails_PermanentCountry");

            migrationBuilder.RenameColumn(
                name: "StudentCommunicationDetails_PermanentCountryName",
                table: "AppStudents",
                newName: "StudentCommunicationDetails_LocalState");

            migrationBuilder.RenameColumn(
                name: "StudentCommunicationDetails_PermanentCountryIsoCode",
                table: "AppStudents",
                newName: "StudentCommunicationDetails_LocalCountry");

            migrationBuilder.RenameColumn(
                name: "StudentCommunicationDetails_PermanentStateName",
                table: "AppStudentEnrollments",
                newName: "StudentCommunicationDetails_PermanentState");

            migrationBuilder.RenameColumn(
                name: "StudentCommunicationDetails_PermanentStateIsoCode",
                table: "AppStudentEnrollments",
                newName: "StudentCommunicationDetails_PermanentCountry");

            migrationBuilder.RenameColumn(
                name: "StudentCommunicationDetails_PermanentCountryName",
                table: "AppStudentEnrollments",
                newName: "StudentCommunicationDetails_LocalState");

            migrationBuilder.RenameColumn(
                name: "StudentCommunicationDetails_PermanentCountryIsoCode",
                table: "AppStudentEnrollments",
                newName: "StudentCommunicationDetails_LocalCountry");
        }
    }
}
