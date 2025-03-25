using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v15 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TemporaryGr",
                table: "AppStudentEnrollments",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApparId",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PAN",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Village",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppStudentGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentGroups", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppStudentGroups");

            migrationBuilder.DropColumn(
                name: "ApparId",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "PAN",
                table: "AppStudentEnrollments");

            migrationBuilder.DropColumn(
                name: "Village",
                table: "AppStudentEnrollments");

            migrationBuilder.AlterColumn<int>(
                name: "TemporaryGr",
                table: "AppStudentEnrollments",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
