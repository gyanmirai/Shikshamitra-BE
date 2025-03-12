using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "InquiryStatusType",
                table: "AppStudentEnrollments",
                newName: "FollowUpStatusType");

            migrationBuilder.AddColumn<int>(
                name: "StudentEnrollmentInquiryState",
                table: "AppStudentEnrollments",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StudentEnrollmentInquiryState",
                table: "AppStudentEnrollments");

            migrationBuilder.RenameColumn(
                name: "FollowUpStatusType",
                table: "AppStudentEnrollments",
                newName: "InquiryStatusType");
        }
    }
}
