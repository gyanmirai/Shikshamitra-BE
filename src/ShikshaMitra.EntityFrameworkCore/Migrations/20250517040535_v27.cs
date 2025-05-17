using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v27 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppSubjects_AppStandards_StandardId",
                table: "AppSubjects");

            migrationBuilder.DropForeignKey(
                name: "FK_AppSubjectTeacher_AppEmployees_TeacherId",
                table: "AppSubjectTeacher");

            migrationBuilder.DropForeignKey(
                name: "FK_AppSubjectTeacher_AppSubjects_SubjectId",
                table: "AppSubjectTeacher");

            migrationBuilder.DropIndex(
                name: "IX_AppSubjects_StandardId",
                table: "AppSubjects");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppSubjectTeacher",
                table: "AppSubjectTeacher");

            migrationBuilder.DropIndex(
                name: "IX_AppSubjectTeacher_TeacherId",
                table: "AppSubjectTeacher");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "AppSubjects");

            migrationBuilder.DropColumn(
                name: "LearningGoals",
                table: "AppSubjects");

            migrationBuilder.DropColumn(
                name: "Prerequisites",
                table: "AppSubjects");

            migrationBuilder.DropColumn(
                name: "ScheduledDate",
                table: "AppSubjects");

            migrationBuilder.DropColumn(
                name: "StandardId",
                table: "AppSubjects");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AppSubjects");

            migrationBuilder.DropColumn(
                name: "SubjectCode",
                table: "AppSubjects");

            migrationBuilder.DropColumn(
                name: "UploadedDocument",
                table: "AppSubjects");

            migrationBuilder.RenameTable(
                name: "AppSubjectTeacher",
                newName: "AppSubjectTeachers");

            migrationBuilder.RenameColumn(
                name: "SubjectName",
                table: "AppSubjects",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "SubjectColor",
                table: "AppSubjects",
                newName: "Color");

            migrationBuilder.AddColumn<Guid>(
                name: "StandardSubjectAssignmentId",
                table: "AppSubjectTeachers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppSubjectTeachers",
                table: "AppSubjectTeachers",
                columns: new[] { "SubjectId", "TeacherId" });

            migrationBuilder.CreateTable(
                name: "AppStandardSubjectAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectCode = table.Column<string>(type: "text", nullable: false),
                    SubjectName = table.Column<string>(type: "text", nullable: false),
                    ScheduledDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubjectColor = table.Column<string>(type: "text", nullable: false),
                    LearningGoals = table.Column<string>(type: "text", nullable: true),
                    Prerequisites = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UploadedDocument = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppStandardSubjectAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStandardSubjectAssignments_AppStandards_StandardId",
                        column: x => x.StandardId,
                        principalTable: "AppStandards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppStandardSubjectAssignments_AppSubjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "AppSubjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppSubjects_AcademicMediumId",
                table: "AppSubjects",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppSubjectTeachers_StandardSubjectAssignmentId",
                table: "AppSubjectTeachers",
                column: "StandardSubjectAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStandardSubjectAssignments_StandardId",
                table: "AppStandardSubjectAssignments",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStandardSubjectAssignments_SubjectId",
                table: "AppStandardSubjectAssignments",
                column: "SubjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppSubjects_AppEducationMediums_AcademicMediumId",
                table: "AppSubjects",
                column: "AcademicMediumId",
                principalTable: "AppEducationMediums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppSubjectTeachers_AppStandardSubjectAssignments_StandardSu~",
                table: "AppSubjectTeachers",
                column: "StandardSubjectAssignmentId",
                principalTable: "AppStandardSubjectAssignments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppSubjects_AppEducationMediums_AcademicMediumId",
                table: "AppSubjects");

            migrationBuilder.DropForeignKey(
                name: "FK_AppSubjectTeachers_AppStandardSubjectAssignments_StandardSu~",
                table: "AppSubjectTeachers");

            migrationBuilder.DropTable(
                name: "AppStandardSubjectAssignments");

            migrationBuilder.DropIndex(
                name: "IX_AppSubjects_AcademicMediumId",
                table: "AppSubjects");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppSubjectTeachers",
                table: "AppSubjectTeachers");

            migrationBuilder.DropIndex(
                name: "IX_AppSubjectTeachers_StandardSubjectAssignmentId",
                table: "AppSubjectTeachers");

            migrationBuilder.DropColumn(
                name: "StandardSubjectAssignmentId",
                table: "AppSubjectTeachers");

            migrationBuilder.RenameTable(
                name: "AppSubjectTeachers",
                newName: "AppSubjectTeacher");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "AppSubjects",
                newName: "SubjectName");

            migrationBuilder.RenameColumn(
                name: "Color",
                table: "AppSubjects",
                newName: "SubjectColor");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "AppSubjects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LearningGoals",
                table: "AppSubjects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Prerequisites",
                table: "AppSubjects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScheduledDate",
                table: "AppSubjects",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "StandardId",
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

            migrationBuilder.AddColumn<string>(
                name: "SubjectCode",
                table: "AppSubjects",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UploadedDocument",
                table: "AppSubjects",
                type: "text",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppSubjectTeacher",
                table: "AppSubjectTeacher",
                columns: new[] { "SubjectId", "TeacherId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppSubjects_StandardId",
                table: "AppSubjects",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppSubjectTeacher_TeacherId",
                table: "AppSubjectTeacher",
                column: "TeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppSubjects_AppStandards_StandardId",
                table: "AppSubjects",
                column: "StandardId",
                principalTable: "AppStandards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppSubjectTeacher_AppEmployees_TeacherId",
                table: "AppSubjectTeacher",
                column: "TeacherId",
                principalTable: "AppEmployees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppSubjectTeacher_AppSubjects_SubjectId",
                table: "AppSubjectTeacher",
                column: "SubjectId",
                principalTable: "AppSubjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
