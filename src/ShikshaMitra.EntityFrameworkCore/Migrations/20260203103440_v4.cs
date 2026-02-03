using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "AppGroupStudentRules_GroupId",
                table: "AppGroupStudentRules");

            migrationBuilder.DropIndex(
                name: "AppGroups_SelectionMode",
                table: "AppGroups");

            migrationBuilder.DropIndex(
                name: "AppGroupEmployeeRules_GroupId",
                table: "AppGroupEmployeeRules");

            migrationBuilder.DropColumn(
                name: "SelectionMode",
                table: "AppGroups");

            migrationBuilder.RenameColumn(
                name: "SelectedDivisionIds",
                table: "AppGroups",
                newName: "RuleBasedRoleIds");

            migrationBuilder.RenameColumn(
                name: "RoleIds",
                table: "AppGroups",
                newName: "RuleBasedDivisionIds");

            migrationBuilder.AddColumn<int>(
                name: "SelectionType",
                table: "AppGroupStudentRules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NameBasedDivisionIds",
                table: "AppGroups",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameBasedRoleIds",
                table: "AppGroups",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SelectionType",
                table: "AppGroupEmployeeRules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "AppGroupStudentRules_GroupId_SelectionType",
                table: "AppGroupStudentRules",
                columns: new[] { "GroupId", "SelectionType" });

            migrationBuilder.CreateIndex(
                name: "AppGroupEmployeeRules_GroupId_SelectionType",
                table: "AppGroupEmployeeRules",
                columns: new[] { "GroupId", "SelectionType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "AppGroupStudentRules_GroupId_SelectionType",
                table: "AppGroupStudentRules");

            migrationBuilder.DropIndex(
                name: "AppGroupEmployeeRules_GroupId_SelectionType",
                table: "AppGroupEmployeeRules");

            migrationBuilder.DropColumn(
                name: "SelectionType",
                table: "AppGroupStudentRules");

            migrationBuilder.DropColumn(
                name: "NameBasedDivisionIds",
                table: "AppGroups");

            migrationBuilder.DropColumn(
                name: "NameBasedRoleIds",
                table: "AppGroups");

            migrationBuilder.DropColumn(
                name: "SelectionType",
                table: "AppGroupEmployeeRules");

            migrationBuilder.RenameColumn(
                name: "RuleBasedRoleIds",
                table: "AppGroups",
                newName: "SelectedDivisionIds");

            migrationBuilder.RenameColumn(
                name: "RuleBasedDivisionIds",
                table: "AppGroups",
                newName: "RoleIds");

            migrationBuilder.AddColumn<int>(
                name: "SelectionMode",
                table: "AppGroups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "AppGroupStudentRules_GroupId",
                table: "AppGroupStudentRules",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "AppGroups_SelectionMode",
                table: "AppGroups",
                column: "SelectionMode");

            migrationBuilder.CreateIndex(
                name: "AppGroupEmployeeRules_GroupId",
                table: "AppGroupEmployeeRules",
                column: "GroupId");
        }
    }
}
