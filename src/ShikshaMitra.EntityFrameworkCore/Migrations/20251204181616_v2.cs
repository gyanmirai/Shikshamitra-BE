using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_EntranceExamSlots",
                table: "EntranceExamSlots");

            migrationBuilder.RenameTable(
                name: "EntranceExamSlots",
                newName: "AppEntranceExamSlots");

            migrationBuilder.AddColumn<Guid>(
                name: "EntranceExamSlotId",
                table: "AppStudentAdditionalDetails",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExamDate",
                table: "AppStudentAdditionalDetails",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "ExamEndTime",
                table: "AppStudentAdditionalDetails",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExamObtainedMarks",
                table: "AppStudentAdditionalDetails",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExamPassingMarks",
                table: "AppStudentAdditionalDetails",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExamPercentage",
                table: "AppStudentAdditionalDetails",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExamResultDate",
                table: "AppStudentAdditionalDetails",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExamRoomNo",
                table: "AppStudentAdditionalDetails",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "ExamStartTime",
                table: "AppStudentAdditionalDetails",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExamStatus",
                table: "AppStudentAdditionalDetails",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExamTotalMarks",
                table: "AppStudentAdditionalDetails",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicYearId",
                table: "AppEntranceExamSlots",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "SlotName",
                table: "AppEntranceExamSlots",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppEntranceExamSlots",
                table: "AppEntranceExamSlots",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_AppEntranceExamSlots_AcademicYearId_StartDate_TenantId",
                table: "AppEntranceExamSlots",
                columns: new[] { "AcademicYearId", "StartDate", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_AppEntranceExamSlots",
                table: "AppEntranceExamSlots");

            migrationBuilder.DropIndex(
                name: "IX_AppEntranceExamSlots_AcademicYearId_StartDate_TenantId",
                table: "AppEntranceExamSlots");

            migrationBuilder.DropColumn(
                name: "EntranceExamSlotId",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "ExamDate",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "ExamEndTime",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "ExamObtainedMarks",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "ExamPassingMarks",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "ExamPercentage",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "ExamResultDate",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "ExamRoomNo",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "ExamStartTime",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "ExamStatus",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "ExamTotalMarks",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "AppEntranceExamSlots");

            migrationBuilder.DropColumn(
                name: "SlotName",
                table: "AppEntranceExamSlots");

            migrationBuilder.RenameTable(
                name: "AppEntranceExamSlots",
                newName: "EntranceExamSlots");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EntranceExamSlots",
                table: "EntranceExamSlots",
                column: "Id");
        }
    }
}
