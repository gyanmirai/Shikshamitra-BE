using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class RenameEmployeeLeavePolicyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppEmployeeLeaves_AppEmployeeLeavePolicies_LeaveTypeId",
                table: "AppEmployeeLeaves");

            migrationBuilder.RenameColumn(
                name: "LeaveTypeId",
                table: "AppEmployeeLeaves",
                newName: "LeavePolicyId");

            migrationBuilder.RenameIndex(
                name: "IX_AppEmployeeLeaves_LeaveTypeId",
                table: "AppEmployeeLeaves",
                newName: "IX_AppEmployeeLeaves_LeavePolicyId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppEmployeeLeaves_AppEmployeeLeavePolicies_LeavePolicyId",
                table: "AppEmployeeLeaves",
                column: "LeavePolicyId",
                principalTable: "AppEmployeeLeavePolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppEmployeeLeaves_AppEmployeeLeavePolicies_LeavePolicyId",
                table: "AppEmployeeLeaves");

            migrationBuilder.RenameColumn(
                name: "LeavePolicyId",
                table: "AppEmployeeLeaves",
                newName: "LeaveTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_AppEmployeeLeaves_LeavePolicyId",
                table: "AppEmployeeLeaves",
                newName: "IX_AppEmployeeLeaves_LeaveTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppEmployeeLeaves_AppEmployeeLeavePolicies_LeaveTypeId",
                table: "AppEmployeeLeaves",
                column: "LeaveTypeId",
                principalTable: "AppEmployeeLeavePolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
