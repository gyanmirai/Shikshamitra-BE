using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v16 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudentDocuments_AppStudentEnrollments_StudentEnrollment~",
                table: "AppStudentDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudentDocuments_AppStudents_StudentId",
                table: "AppStudentDocuments");

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "AppStudentDocuments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentEnrollmentId",
                table: "AppStudentDocuments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StudentEnrollmentId1",
                table: "AppStudentDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentDocuments_StudentEnrollmentId1",
                table: "AppStudentDocuments",
                column: "StudentEnrollmentId1");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudentDocuments_AppStudentEnrollments_StudentEnrollment~",
                table: "AppStudentDocuments",
                column: "StudentEnrollmentId",
                principalTable: "AppStudentEnrollments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudentDocuments_AppStudentEnrollments_StudentEnrollmen~1",
                table: "AppStudentDocuments",
                column: "StudentEnrollmentId1",
                principalTable: "AppStudentEnrollments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudentDocuments_AppStudents_StudentId",
                table: "AppStudentDocuments",
                column: "StudentId",
                principalTable: "AppStudents",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudentDocuments_AppStudentEnrollments_StudentEnrollment~",
                table: "AppStudentDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudentDocuments_AppStudentEnrollments_StudentEnrollmen~1",
                table: "AppStudentDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudentDocuments_AppStudents_StudentId",
                table: "AppStudentDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentDocuments_StudentEnrollmentId1",
                table: "AppStudentDocuments");

            migrationBuilder.DropColumn(
                name: "StudentEnrollmentId1",
                table: "AppStudentDocuments");

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "AppStudentDocuments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentEnrollmentId",
                table: "AppStudentDocuments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudentDocuments_AppStudentEnrollments_StudentEnrollment~",
                table: "AppStudentDocuments",
                column: "StudentEnrollmentId",
                principalTable: "AppStudentEnrollments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudentDocuments_AppStudents_StudentId",
                table: "AppStudentDocuments",
                column: "StudentId",
                principalTable: "AppStudents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
