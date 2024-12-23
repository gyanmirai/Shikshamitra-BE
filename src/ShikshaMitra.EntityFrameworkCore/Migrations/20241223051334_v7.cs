using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gender",
                table: "AppStudents");

            migrationBuilder.AddColumn<int>(
                name: "GenderType",
                table: "AppStudents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "AppEmployees",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangeReason",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EducationMediumStageId",
                table: "AppEmployees",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StatusLastChangedAt",
                table: "AppEmployees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRequired",
                table: "AppDocumentSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployees_EducationMediumStageId",
                table: "AppEmployees",
                column: "EducationMediumStageId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppEmployees_AppEducationMediumStages_EducationMediumStageId",
                table: "AppEmployees",
                column: "EducationMediumStageId",
                principalTable: "AppEducationMediumStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppEmployees_AppEducationMediumStages_EducationMediumStageId",
                table: "AppEmployees");

            migrationBuilder.DropIndex(
                name: "IX_AppEmployees_EducationMediumStageId",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "GenderType",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "ChangeReason",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "EducationMediumStageId",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "StatusLastChangedAt",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "IsRequired",
                table: "AppDocumentSettings");

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "AppStudents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "AppEmployees",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }
    }
}
