using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v20 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppSubjects_AppEmployees_PrimaryTeacherId",
                table: "AppSubjects");

            migrationBuilder.DropIndex(
                name: "IX_AppSubjects_PrimaryTeacherId",
                table: "AppSubjects");

            migrationBuilder.DropColumn(
                name: "PrimaryTeacherId",
                table: "AppSubjects");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ScheduledDate",
                table: "AppSubjects",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BoardId",
                table: "AppSubjects",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "MediumId",
                table: "AppSubjects",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "AppSubjects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "YearId",
                table: "AppSubjects",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "AppSubjectTeacher",
                columns: table => new
                {
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherType = table.Column<int>(type: "integer", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSubjectTeacher", x => new { x.SubjectId, x.TeacherId });
                    table.ForeignKey(
                        name: "FK_AppSubjectTeacher_AppEmployees_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "AppEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppSubjectTeacher_AppSubjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "AppSubjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppSubjectTeacher_TeacherId",
                table: "AppSubjectTeacher",
                column: "TeacherId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSubjectTeacher");

            migrationBuilder.DropColumn(
                name: "BoardId",
                table: "AppSubjects");

            migrationBuilder.DropColumn(
                name: "MediumId",
                table: "AppSubjects");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AppSubjects");

            migrationBuilder.DropColumn(
                name: "YearId",
                table: "AppSubjects");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ScheduledDate",
                table: "AppSubjects",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryTeacherId",
                table: "AppSubjects",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppSubjects_PrimaryTeacherId",
                table: "AppSubjects",
                column: "PrimaryTeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppSubjects_AppEmployees_PrimaryTeacherId",
                table: "AppSubjects",
                column: "PrimaryTeacherId",
                principalTable: "AppEmployees",
                principalColumn: "Id");
        }
    }
}
