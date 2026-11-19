using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations;

public partial class MobileAcademicResponses : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<string>("MobileAttachmentsJson", "AppStudentLeaves", type: "text", nullable: true);
		migrationBuilder.AddColumn<TimeOnly>("HalfDayStartTime", "AppStudentLeaves", type: "time without time zone", nullable: true);
		migrationBuilder.AddColumn<TimeOnly>("HalfDayEndTime", "AppStudentLeaves", type: "time without time zone", nullable: true);
		migrationBuilder.AddColumn<string>("GuardianAbsenceReason", "AppAttendanceEntries", type: "text", nullable: true);
		migrationBuilder.AddColumn<string>("MobileAttachmentsJson", "academic_StudentHomeworkAssignmentSubmissions", type: "text", nullable: true);
	}
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn("MobileAttachmentsJson", "AppStudentLeaves");
		migrationBuilder.DropColumn("HalfDayStartTime", "AppStudentLeaves");
		migrationBuilder.DropColumn("HalfDayEndTime", "AppStudentLeaves");
		migrationBuilder.DropColumn("GuardianAbsenceReason", "AppAttendanceEntries");
		migrationBuilder.DropColumn("MobileAttachmentsJson", "academic_StudentHomeworkAssignmentSubmissions");
	}
}
