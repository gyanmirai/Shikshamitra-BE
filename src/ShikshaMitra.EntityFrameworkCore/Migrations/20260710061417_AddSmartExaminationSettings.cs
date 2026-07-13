using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartExaminationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "AppGradingScales",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "AppGradingScales",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "PassMarks",
                table: "AppGradingScales",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "AppGradingScales",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "GradePoint",
                table: "AppGradeRanges",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remark",
                table: "AppGradeRanges",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResultStatus",
                table: "AppGradeRanges",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "AllowPracticalOral",
                table: "AppExamTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "AppExamTypes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultGradeScaleId",
                table: "AppExamTypes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DefaultTotalMarks",
                table: "AppExamTypes",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DefaultWeightage",
                table: "AppExamTypes",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GradeScaleRequired",
                table: "AppExamTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IncludedInResult",
                table: "AppExamTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MarksEntryRequired",
                table: "AppExamTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "AppExamTypes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "AppGradingScales");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "AppGradingScales");

            migrationBuilder.DropColumn(
                name: "PassMarks",
                table: "AppGradingScales");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AppGradingScales");

            migrationBuilder.DropColumn(
                name: "GradePoint",
                table: "AppGradeRanges");

            migrationBuilder.DropColumn(
                name: "Remark",
                table: "AppGradeRanges");

            migrationBuilder.DropColumn(
                name: "ResultStatus",
                table: "AppGradeRanges");

            migrationBuilder.DropColumn(
                name: "AllowPracticalOral",
                table: "AppExamTypes");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "AppExamTypes");

            migrationBuilder.DropColumn(
                name: "DefaultGradeScaleId",
                table: "AppExamTypes");

            migrationBuilder.DropColumn(
                name: "DefaultTotalMarks",
                table: "AppExamTypes");

            migrationBuilder.DropColumn(
                name: "DefaultWeightage",
                table: "AppExamTypes");

            migrationBuilder.DropColumn(
                name: "GradeScaleRequired",
                table: "AppExamTypes");

            migrationBuilder.DropColumn(
                name: "IncludedInResult",
                table: "AppExamTypes");

            migrationBuilder.DropColumn(
                name: "MarksEntryRequired",
                table: "AppExamTypes");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AppExamTypes");
        }
    }
}
