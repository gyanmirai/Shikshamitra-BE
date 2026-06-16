using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class StudentBeTaskCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SchoolIndexNumber",
                table: "StudentPreviousSchoolDetails",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisabilityDetails",
                table: "AppStudentAdditionalDetails",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                table: "AppLectureSlotTimeSlots",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppAcademicTerms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAcademicTerms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAcademicTerms_AppAcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AppAcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentRollNumberAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicTermId = table.Column<Guid>(type: "uuid", nullable: false),
                    RollNo = table.Column<int>(type: "integer", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentRollNumberAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentRollNumberAssignments_AppAcademicTerms_AcademicTe~",
                        column: x => x.AcademicTermId,
                        principalTable: "AppAcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentRollNumberAssignments_AppDivisions_DivisionId",
                        column: x => x.DivisionId,
                        principalTable: "AppDivisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentRollNumberAssignments_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppLectureSlotTimeSlots_RoomId",
                table: "AppLectureSlotTimeSlots",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicTerms_AcademicYearId",
                table: "AppAcademicTerms",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicTerms_TenantId_AcademicYearId_IsCurrent",
                table: "AppAcademicTerms",
                columns: new[] { "TenantId", "AcademicYearId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicTerms_TenantId_AcademicYearId_Sequence",
                table: "AppAcademicTerms",
                columns: new[] { "TenantId", "AcademicYearId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRollNumberAssignments_AcademicTermId",
                table: "AppStudentRollNumberAssignments",
                column: "AcademicTermId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRollNumberAssignments_DivisionId",
                table: "AppStudentRollNumberAssignments",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRollNumberAssignments_StudentId",
                table: "AppStudentRollNumberAssignments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRollNumberAssignments_TenantId_AcademicTermId_Div~",
                table: "AppStudentRollNumberAssignments",
                columns: new[] { "TenantId", "AcademicTermId", "DivisionId", "RollNo" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRollNumberAssignments_TenantId_AcademicTermId_Stu~",
                table: "AppStudentRollNumberAssignments",
                columns: new[] { "TenantId", "AcademicTermId", "StudentId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppLectureSlotTimeSlots_AppRooms_RoomId",
                table: "AppLectureSlotTimeSlots",
                column: "RoomId",
                principalTable: "AppRooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppLectureSlotTimeSlots_AppRooms_RoomId",
                table: "AppLectureSlotTimeSlots");

            migrationBuilder.DropTable(
                name: "AppStudentRollNumberAssignments");

            migrationBuilder.DropTable(
                name: "AppAcademicTerms");

            migrationBuilder.DropIndex(
                name: "IX_AppLectureSlotTimeSlots_RoomId",
                table: "AppLectureSlotTimeSlots");

            migrationBuilder.DropColumn(
                name: "SchoolIndexNumber",
                table: "StudentPreviousSchoolDetails");

            migrationBuilder.DropColumn(
                name: "DisabilityDetails",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "AppLectureSlotTimeSlots");
        }
    }
}
