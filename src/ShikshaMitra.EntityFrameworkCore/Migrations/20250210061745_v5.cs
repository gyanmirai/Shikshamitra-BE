using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StudentBasicDetails_UserId",
                table: "AppStudentEnrollments",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "StudentBasicDetails_TemporaryGr",
                table: "AppStudentEnrollments",
                newName: "TemporaryGr");

            migrationBuilder.RenameColumn(
                name: "StudentBasicDetails_Remarks",
                table: "AppStudentEnrollments",
                newName: "Remarks");

            migrationBuilder.RenameColumn(
                name: "StudentBasicDetails_Religion",
                table: "AppStudentEnrollments",
                newName: "Religion");

            migrationBuilder.RenameColumn(
                name: "StudentBasicDetails_ProfilePicture",
                table: "AppStudentEnrollments",
                newName: "ProfilePicture");

            migrationBuilder.RenameColumn(
                name: "StudentBasicDetails_Nationality",
                table: "AppStudentEnrollments",
                newName: "Nationality");

            migrationBuilder.RenameColumn(
                name: "StudentBasicDetails_MotherTongue",
                table: "AppStudentEnrollments",
                newName: "MotherTongue");

            migrationBuilder.RenameColumn(
                name: "StudentBasicDetails_FamilyCast",
                table: "AppStudentEnrollments",
                newName: "FamilyCast");

            migrationBuilder.RenameColumn(
                name: "StudentBasicDetails_Email",
                table: "AppStudentEnrollments",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "StudentBasicDetails_CastType",
                table: "AppStudentEnrollments",
                newName: "CastType");

            migrationBuilder.RenameColumn(
                name: "StudentBasicDetails_BirthPlace",
                table: "AppStudentEnrollments",
                newName: "BirthPlace");

            migrationBuilder.RenameColumn(
                name: "StudentBasicDetails_AadhaarNo",
                table: "AppStudentEnrollments",
                newName: "AadhaarNo");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAttendances_StudentId",
                table: "AppStudentAttendances",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudentAttendances_AppStudents_StudentId",
                table: "AppStudentAttendances",
                column: "StudentId",
                principalTable: "AppStudents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudentAttendances_AppStudents_StudentId",
                table: "AppStudentAttendances");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentAttendances_StudentId",
                table: "AppStudentAttendances");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "AppStudentEnrollments",
                newName: "StudentBasicDetails_UserId");

            migrationBuilder.RenameColumn(
                name: "TemporaryGr",
                table: "AppStudentEnrollments",
                newName: "StudentBasicDetails_TemporaryGr");

            migrationBuilder.RenameColumn(
                name: "Remarks",
                table: "AppStudentEnrollments",
                newName: "StudentBasicDetails_Remarks");

            migrationBuilder.RenameColumn(
                name: "Religion",
                table: "AppStudentEnrollments",
                newName: "StudentBasicDetails_Religion");

            migrationBuilder.RenameColumn(
                name: "ProfilePicture",
                table: "AppStudentEnrollments",
                newName: "StudentBasicDetails_ProfilePicture");

            migrationBuilder.RenameColumn(
                name: "Nationality",
                table: "AppStudentEnrollments",
                newName: "StudentBasicDetails_Nationality");

            migrationBuilder.RenameColumn(
                name: "MotherTongue",
                table: "AppStudentEnrollments",
                newName: "StudentBasicDetails_MotherTongue");

            migrationBuilder.RenameColumn(
                name: "FamilyCast",
                table: "AppStudentEnrollments",
                newName: "StudentBasicDetails_FamilyCast");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "AppStudentEnrollments",
                newName: "StudentBasicDetails_Email");

            migrationBuilder.RenameColumn(
                name: "CastType",
                table: "AppStudentEnrollments",
                newName: "StudentBasicDetails_CastType");

            migrationBuilder.RenameColumn(
                name: "BirthPlace",
                table: "AppStudentEnrollments",
                newName: "StudentBasicDetails_BirthPlace");

            migrationBuilder.RenameColumn(
                name: "AadhaarNo",
                table: "AppStudentEnrollments",
                newName: "StudentBasicDetails_AadhaarNo");
        }
    }
}
