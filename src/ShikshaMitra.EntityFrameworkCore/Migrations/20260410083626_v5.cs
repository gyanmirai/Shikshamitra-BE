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
            migrationBuilder.DropForeignKey(
                name: "FK_AppEmployees_AppPayrollStructures_PayrollStructureId",
                table: "AppEmployees");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppInquiryPreferences_InquiryPreferenceId",
                table: "AppStudents");

            migrationBuilder.DropTable(
                name: "AppEventAndHolidays");

            migrationBuilder.DropTable(
                name: "AppPayrollStructures");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_InquiryPreferenceId",
                table: "AppStudents");

            migrationBuilder.DropIndex(
                name: "IX_AppEmployees_PayrollStructureId",
                table: "AppEmployees");

            migrationBuilder.DropIndex(
                name: "IX_EarningComponents_TenantId_Name",
                table: "AppEarningComponents");

            migrationBuilder.DropIndex(
                name: "IX_DeductionComponents_TenantId_Name",
                table: "AppDeductionComponents");

            migrationBuilder.DropColumn(
                name: "InquiryPreferenceId",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "ExamEndTime",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "ExamStartTime",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "PayrollStructureId",
                table: "AppEmployees");

            migrationBuilder.RenameColumn(
                name: "ExamTotalMarks",
                table: "AppStudentAdditionalDetails",
                newName: "EntranceExamTotalMarks");

            migrationBuilder.RenameColumn(
                name: "ExamStatus",
                table: "AppStudentAdditionalDetails",
                newName: "EntranceExamStatus");

            migrationBuilder.RenameColumn(
                name: "ExamRoomNo",
                table: "AppStudentAdditionalDetails",
                newName: "EntranceExamRoomNo");

            migrationBuilder.RenameColumn(
                name: "ExamResultDate",
                table: "AppStudentAdditionalDetails",
                newName: "EntranceExamStartDateTime");

            migrationBuilder.RenameColumn(
                name: "ExamPercentage",
                table: "AppStudentAdditionalDetails",
                newName: "EntranceExamPercentage");

            migrationBuilder.RenameColumn(
                name: "ExamPassingMarks",
                table: "AppStudentAdditionalDetails",
                newName: "EntranceExamPassingMarks");

            migrationBuilder.RenameColumn(
                name: "ExamObtainedMarks",
                table: "AppStudentAdditionalDetails",
                newName: "EntranceExamObtainedMarks");

            migrationBuilder.RenameColumn(
                name: "ExamDate",
                table: "AppStudentAdditionalDetails",
                newName: "EntranceExamResultDate");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EntranceExamEndDateTime",
                table: "AppStudentAdditionalDetails",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "AppProvidentFunds",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "AppProvidentFunds",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAdditionalDetails_InquiryPreferenceId",
                table: "AppStudentAdditionalDetails",
                column: "InquiryPreferenceId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudentAdditionalDetails_AppInquiryPreferences_InquiryPr~",
                table: "AppStudentAdditionalDetails",
                column: "InquiryPreferenceId",
                principalTable: "AppInquiryPreferences",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppAcademicYears_AcademicYearId",
                table: "AppStudents",
                column: "AcademicYearId",
                principalTable: "AppAcademicYears",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudentAdditionalDetails_AppInquiryPreferences_InquiryPr~",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppAcademicYears_AcademicYearId",
                table: "AppStudents");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentAdditionalDetails_InquiryPreferenceId",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropIndex(
                name: "IX_ProvidentFunds_TenantId_Code",
                table: "AppProvidentFunds");

            migrationBuilder.DropIndex(
                name: "IX_EarningComponents_TenantId_Name",
                table: "AppEarningComponents");

            migrationBuilder.DropIndex(
                name: "IX_DeductionComponents_TenantId_Name",
                table: "AppDeductionComponents");

            migrationBuilder.DropColumn(
                name: "EntranceExamEndDateTime",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.RenameColumn(
                name: "EntranceExamTotalMarks",
                table: "AppStudentAdditionalDetails",
                newName: "ExamTotalMarks");

            migrationBuilder.RenameColumn(
                name: "EntranceExamStatus",
                table: "AppStudentAdditionalDetails",
                newName: "ExamStatus");

            migrationBuilder.RenameColumn(
                name: "EntranceExamStartDateTime",
                table: "AppStudentAdditionalDetails",
                newName: "ExamResultDate");

            migrationBuilder.RenameColumn(
                name: "EntranceExamRoomNo",
                table: "AppStudentAdditionalDetails",
                newName: "ExamRoomNo");

            migrationBuilder.RenameColumn(
                name: "EntranceExamResultDate",
                table: "AppStudentAdditionalDetails",
                newName: "ExamDate");

            migrationBuilder.RenameColumn(
                name: "EntranceExamPercentage",
                table: "AppStudentAdditionalDetails",
                newName: "ExamPercentage");

            migrationBuilder.RenameColumn(
                name: "EntranceExamPassingMarks",
                table: "AppStudentAdditionalDetails",
                newName: "ExamPassingMarks");

            migrationBuilder.RenameColumn(
                name: "EntranceExamObtainedMarks",
                table: "AppStudentAdditionalDetails",
                newName: "ExamObtainedMarks");

            migrationBuilder.AddColumn<Guid>(
                name: "InquiryPreferenceId",
                table: "AppStudents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "ExamEndTime",
                table: "AppStudentAdditionalDetails",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "ExamStartTime",
                table: "AppStudentAdditionalDetails",
                type: "interval",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "AppProvidentFunds",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "AppProvidentFunds",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<Guid>(
                name: "PayrollStructureId",
                table: "AppEmployees",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppEventAndHolidays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsHoliday = table.Column<bool>(type: "boolean", nullable: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEventAndHolidays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPayrollStructures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationMediumStageId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayElementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<double>(type: "double precision", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayElementType = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPayrollStructures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppPayrollStructures_AppEducationMediumStages_EducationMedi~",
                        column: x => x.EducationMediumStageId,
                        principalTable: "AppEducationMediumStages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppPayrollStructures_AppPayElements_PayElementId",
                        column: x => x.PayElementId,
                        principalTable: "AppPayElements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_InquiryPreferenceId",
                table: "AppStudents",
                column: "InquiryPreferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployees_PayrollStructureId",
                table: "AppEmployees",
                column: "PayrollStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_EarningComponents_TenantId_Name",
                table: "AppEarningComponents",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeductionComponents_TenantId_Name",
                table: "AppDeductionComponents",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppPayrollStructures_EducationMediumStageId",
                table: "AppPayrollStructures",
                column: "EducationMediumStageId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPayrollStructures_PayElementId",
                table: "AppPayrollStructures",
                column: "PayElementId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppEmployees_AppPayrollStructures_PayrollStructureId",
                table: "AppEmployees",
                column: "PayrollStructureId",
                principalTable: "AppPayrollStructures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppInquiryPreferences_InquiryPreferenceId",
                table: "AppStudents",
                column: "InquiryPreferenceId",
                principalTable: "AppInquiryPreferences",
                principalColumn: "Id");
        }
    }
}
