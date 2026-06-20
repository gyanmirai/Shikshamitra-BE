using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAttendancePolicySandwichRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SandwichRuleEnabled",
                table: "HR_AttendancePolicies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SandwichRuleEnabled",
                table: "HR_AttendancePolicies",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
