using System;
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
                name: "State",
                table: "AppStudentEnrollments",
                newName: "StateName");

            migrationBuilder.RenameColumn(
                name: "Country",
                table: "AppStudentEnrollments",
                newName: "StateIsoCode");

            migrationBuilder.AddColumn<string>(
                name: "CountryIsoCode",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryName",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FeeStatusType",
                table: "AppStudentEnrollments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<float>(
                name: "PaidAmount",
                table: "AppStudentEnrollments",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<int>(
                name: "StudentEnrollmentApprovalState",
                table: "AppStudentEnrollments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<float>(
                name: "TotalAmount",
                table: "AppStudentEnrollments",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<int>(
                name: "AgeLowerLimit",
                table: "AppStandards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RoomNumber",
                table: "AppDivisions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AppShiftManagements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftName = table.Column<string>(type: "text", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppShiftManagements", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppShiftManagements");

            migrationBuilder.DropColumn(
                name: "CountryIsoCode",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "CountryName",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "FeeStatusType",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "StudentEnrollmentApprovalState",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "AgeLowerLimit",
                table: "AppStandards");

            migrationBuilder.DropColumn(
                name: "RoomNumber",
                table: "AppDivisions");

            migrationBuilder.RenameColumn(
                name: "StateName",
                table: "AppStudentEnrollments",
                newName: "State");

            migrationBuilder.RenameColumn(
                name: "StateIsoCode",
                table: "AppStudentEnrollments",
                newName: "Country");
        }
    }
}
