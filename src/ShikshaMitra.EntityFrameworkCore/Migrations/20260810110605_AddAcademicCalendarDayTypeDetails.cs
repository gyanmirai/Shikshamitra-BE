using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicCalendarDayTypeDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "ExamEndTime",
                table: "AppAcademicCalendarDays",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "ExamStartTime",
                table: "AppAcademicCalendarDays",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExamTypeId",
                table: "AppAcademicCalendarDays",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LinkedExamId",
                table: "AppAcademicCalendarDays",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeetingLink",
                table: "AppAcademicCalendarDays",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PtmMode",
                table: "AppAcademicCalendarDays",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                table: "AppAcademicCalendarDays",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Venue",
                table: "AppAcademicCalendarDays",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExamEndTime",
                table: "AppAcademicCalendarDays");

            migrationBuilder.DropColumn(
                name: "ExamStartTime",
                table: "AppAcademicCalendarDays");

            migrationBuilder.DropColumn(
                name: "ExamTypeId",
                table: "AppAcademicCalendarDays");

            migrationBuilder.DropColumn(
                name: "LinkedExamId",
                table: "AppAcademicCalendarDays");

            migrationBuilder.DropColumn(
                name: "MeetingLink",
                table: "AppAcademicCalendarDays");

            migrationBuilder.DropColumn(
                name: "PtmMode",
                table: "AppAcademicCalendarDays");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "AppAcademicCalendarDays");

            migrationBuilder.DropColumn(
                name: "Venue",
                table: "AppAcademicCalendarDays");
        }
    }
}
