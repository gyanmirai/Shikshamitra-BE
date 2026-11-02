using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HouseId",
                table: "AppStudentEnrollments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaderEnrollmentId",
                table: "AppHouseSettings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MembershipRevision",
                table: "AppHouseSettings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaderEnrollmentId",
                table: "academic_Divisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LeadershipRevision",
                table: "academic_Divisions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentEnrollments_HouseId",
                table: "AppStudentEnrollments",
                column: "HouseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentEnrollments_TenantId_HouseId",
                table: "AppStudentEnrollments",
                columns: new[] { "TenantId", "HouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppHouseSettings_LeaderEnrollmentId",
                table: "AppHouseSettings",
                column: "LeaderEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_academic_Divisions_LeaderEnrollmentId",
                table: "academic_Divisions",
                column: "LeaderEnrollmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_academic_Divisions_AppStudentEnrollments_LeaderEnrollmentId",
                table: "academic_Divisions",
                column: "LeaderEnrollmentId",
                principalTable: "AppStudentEnrollments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppHouseSettings_AppStudentEnrollments_LeaderEnrollmentId",
                table: "AppHouseSettings",
                column: "LeaderEnrollmentId",
                principalTable: "AppStudentEnrollments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudentEnrollments_AppHouseSettings_HouseId",
                table: "AppStudentEnrollments",
                column: "HouseId",
                principalTable: "AppHouseSettings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_academic_Divisions_AppStudentEnrollments_LeaderEnrollmentId",
                table: "academic_Divisions");

            migrationBuilder.DropForeignKey(
                name: "FK_AppHouseSettings_AppStudentEnrollments_LeaderEnrollmentId",
                table: "AppHouseSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudentEnrollments_AppHouseSettings_HouseId",
                table: "AppStudentEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentEnrollments_HouseId",
                table: "AppStudentEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentEnrollments_TenantId_HouseId",
                table: "AppStudentEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_AppHouseSettings_LeaderEnrollmentId",
                table: "AppHouseSettings");

            migrationBuilder.DropIndex(
                name: "IX_academic_Divisions_LeaderEnrollmentId",
                table: "academic_Divisions");

            migrationBuilder.DropColumn(
                name: "HouseId",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "LeaderEnrollmentId",
                table: "AppHouseSettings");

            migrationBuilder.DropColumn(
                name: "MembershipRevision",
                table: "AppHouseSettings");

            migrationBuilder.DropColumn(
                name: "LeaderEnrollmentId",
                table: "academic_Divisions");

            migrationBuilder.DropColumn(
                name: "LeadershipRevision",
                table: "academic_Divisions");
        }
    }
}
