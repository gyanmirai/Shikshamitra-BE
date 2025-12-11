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
            migrationBuilder.DropForeignKey(
                name: "FK_AppHomeworkAssignments_AppHomeworkAssignmentTypes_HomeworkA~",
                table: "AppHomeworkAssignments");

            migrationBuilder.RenameIndex(
                name: "IX_AppStudentHomeworkAssignmentSubmissions_HomeworkAssignmentId",
                table: "AppStudentHomeworkAssignmentSubmissions",
                newName: "IX_StudentSubmissions_HomeworkAssignmentId");

            migrationBuilder.RenameIndex(
                name: "IX_AppHomeworkAssignments_HomeworkAssignmentTypeId",
                table: "AppHomeworkAssignments",
                newName: "IX_HomeworkAssignments_HomeworkAssignmentTypeId");

            migrationBuilder.AlterColumn<string>(
                name: "SubmittedDocumentUrl",
                table: "AppStudentHomeworkAssignmentSubmissions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Marks",
                table: "AppStudentHomeworkAssignmentSubmissions",
                type: "numeric(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Feedback",
                table: "AppStudentHomeworkAssignmentSubmissions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignmentId",
                table: "AppStudentHomeworkAssignmentSubmissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DivisionId",
                table: "AppStudentHomeworkAssignmentSubmissions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "AppStudentHomeworkAssignmentSubmissions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "AppStudentHomeworkAssignmentSubmissions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "AppStudentHomeworkAssignmentSubmissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedBy",
                table: "AppStudentHomeworkAssignmentSubmissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmittedContent",
                table: "AppStudentHomeworkAssignmentSubmissions",
                type: "text",
                maxLength: 2147483647,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalMarks",
                table: "AppStudentHomeworkAssignmentSubmissions",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "AppHomeworkAssignments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<bool>(
                name: "IsPublished",
                table: "AppHomeworkAssignments",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<Guid>(
                name: "HomeworkAssignmentTypeId",
                table: "AppHomeworkAssignments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DueDate",
                table: "AppHomeworkAssignments",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "DivisionId",
                table: "AppHomeworkAssignments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "AttachmentsJson",
                table: "AppHomeworkAssignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChapterIdsJson",
                table: "AppHomeworkAssignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HomeworkTypeId",
                table: "AppHomeworkAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppAssignmentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AppAssignmentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAssignmentTypes_AppEducationMediums_AcademicMediumId",
                        column: x => x.AcademicMediumId,
                        principalTable: "AppEducationMediums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppHomeworkDivisionSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    HomeworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppHomeworkDivisionSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppHomeworkDivisionSchedules_AppHomeworkAssignments_Homewor~",
                        column: x => x.HomeworkId,
                        principalTable: "AppHomeworkAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppHomeworkTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AppHomeworkTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppHomeworkTypes_AppEducationMediums_AcademicMediumId",
                        column: x => x.AcademicMediumId,
                        principalTable: "AppEducationMediums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationBoardId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumStageId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    AssignmentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionType = table.Column<int>(type: "integer", nullable: false),
                    ChapterIdsJson = table.Column<string>(type: "text", nullable: true),
                    AttachmentsJson = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_AppAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAssignments_AppAssignmentTypes_AssignmentTypeId",
                        column: x => x.AssignmentTypeId,
                        principalTable: "AppAssignmentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppAssignmentDivisionSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAssignmentDivisionSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAssignmentDivisionSchedules_AppAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "AppAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentSubmissions_AssignmentId",
                table: "AppStudentHomeworkAssignmentSubmissions",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSubmissions_DivisionId",
                table: "AppStudentHomeworkAssignmentSubmissions",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSubmissions_Status",
                table: "AppStudentHomeworkAssignmentSubmissions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSubmissions_Student_Assignment",
                table: "AppStudentHomeworkAssignmentSubmissions",
                columns: new[] { "StudentId", "AssignmentId" },
                unique: true,
                filter: "\"AssignmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSubmissions_Student_Homework",
                table: "AppStudentHomeworkAssignmentSubmissions",
                columns: new[] { "StudentId", "HomeworkAssignmentId" },
                unique: true,
                filter: "\"AssignmentId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSubmissions_StudentId",
                table: "AppStudentHomeworkAssignmentSubmissions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_AcademicYearId",
                table: "AppHomeworkAssignments",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_HomeworkTypeId",
                table: "AppHomeworkAssignments",
                column: "HomeworkTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_StandardId",
                table: "AppHomeworkAssignments",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_SubjectId",
                table: "AppHomeworkAssignments",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkAssignments_TenantId",
                table: "AppHomeworkAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentDivisionSchedules_AssignmentId",
                table: "AppAssignmentDivisionSchedules",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentDivisionSchedules_AssignmentId_DivisionId",
                table: "AppAssignmentDivisionSchedules",
                columns: new[] { "AssignmentId", "DivisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentDivisionSchedules_DivisionId",
                table: "AppAssignmentDivisionSchedules",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentDivisionSchedules_PublishDate",
                table: "AppAssignmentDivisionSchedules",
                column: "PublishDate",
                filter: "\"PublishDate\" IS NOT NULL AND \"IsPublished\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_AcademicYearId",
                table: "AppAssignments",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_AssignmentTypeId",
                table: "AppAssignments",
                column: "AssignmentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_StandardId",
                table: "AppAssignments",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_SubjectId",
                table: "AppAssignments",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_Tenant_Standard_Subject",
                table: "AppAssignments",
                columns: new[] { "TenantId", "StandardId", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_TenantId",
                table: "AppAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentTypes_AcademicMediumId_Name",
                table: "AppAssignmentTypes",
                columns: new[] { "AcademicMediumId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkDivisionSchedules_DivisionId",
                table: "AppHomeworkDivisionSchedules",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkDivisionSchedules_HomeworkId",
                table: "AppHomeworkDivisionSchedules",
                column: "HomeworkId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkDivisionSchedules_HomeworkId_DivisionId",
                table: "AppHomeworkDivisionSchedules",
                columns: new[] { "HomeworkId", "DivisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkDivisionSchedules_PublishDate",
                table: "AppHomeworkDivisionSchedules",
                column: "PublishDate",
                filter: "\"PublishDate\" IS NOT NULL AND \"IsPublished\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkTypes_AcademicMediumId_Name",
                table: "AppHomeworkTypes",
                columns: new[] { "AcademicMediumId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppHomeworkAssignments_AppHomeworkAssignmentTypes_HomeworkA~",
                table: "AppHomeworkAssignments",
                column: "HomeworkAssignmentTypeId",
                principalTable: "AppHomeworkAssignmentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppHomeworkAssignments_AppHomeworkTypes_HomeworkTypeId",
                table: "AppHomeworkAssignments",
                column: "HomeworkTypeId",
                principalTable: "AppHomeworkTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudentHomeworkAssignmentSubmissions_AppAssignments_Assi~",
                table: "AppStudentHomeworkAssignmentSubmissions",
                column: "AssignmentId",
                principalTable: "AppAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppHomeworkAssignments_AppHomeworkAssignmentTypes_HomeworkA~",
                table: "AppHomeworkAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppHomeworkAssignments_AppHomeworkTypes_HomeworkTypeId",
                table: "AppHomeworkAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudentHomeworkAssignmentSubmissions_AppAssignments_Assi~",
                table: "AppStudentHomeworkAssignmentSubmissions");

            migrationBuilder.DropTable(
                name: "AppAssignmentDivisionSchedules");

            migrationBuilder.DropTable(
                name: "AppHomeworkDivisionSchedules");

            migrationBuilder.DropTable(
                name: "AppHomeworkTypes");

            migrationBuilder.DropTable(
                name: "AppAssignments");

            migrationBuilder.DropTable(
                name: "AppAssignmentTypes");

            migrationBuilder.DropIndex(
                name: "IX_StudentSubmissions_AssignmentId",
                table: "AppStudentHomeworkAssignmentSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_StudentSubmissions_DivisionId",
                table: "AppStudentHomeworkAssignmentSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_StudentSubmissions_Status",
                table: "AppStudentHomeworkAssignmentSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_StudentSubmissions_Student_Assignment",
                table: "AppStudentHomeworkAssignmentSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_StudentSubmissions_Student_Homework",
                table: "AppStudentHomeworkAssignmentSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_StudentSubmissions_StudentId",
                table: "AppStudentHomeworkAssignmentSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_HomeworkAssignments_AcademicYearId",
                table: "AppHomeworkAssignments");

            migrationBuilder.DropIndex(
                name: "IX_HomeworkAssignments_HomeworkTypeId",
                table: "AppHomeworkAssignments");

            migrationBuilder.DropIndex(
                name: "IX_HomeworkAssignments_StandardId",
                table: "AppHomeworkAssignments");

            migrationBuilder.DropIndex(
                name: "IX_HomeworkAssignments_SubjectId",
                table: "AppHomeworkAssignments");

            migrationBuilder.DropIndex(
                name: "IX_HomeworkAssignments_TenantId",
                table: "AppHomeworkAssignments");

            migrationBuilder.DropColumn(
                name: "AssignmentId",
                table: "AppStudentHomeworkAssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "DivisionId",
                table: "AppStudentHomeworkAssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "AppStudentHomeworkAssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "AppStudentHomeworkAssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "AppStudentHomeworkAssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "AppStudentHomeworkAssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "SubmittedContent",
                table: "AppStudentHomeworkAssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "TotalMarks",
                table: "AppStudentHomeworkAssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "AttachmentsJson",
                table: "AppHomeworkAssignments");

            migrationBuilder.DropColumn(
                name: "ChapterIdsJson",
                table: "AppHomeworkAssignments");

            migrationBuilder.DropColumn(
                name: "HomeworkTypeId",
                table: "AppHomeworkAssignments");

            migrationBuilder.RenameIndex(
                name: "IX_StudentSubmissions_HomeworkAssignmentId",
                table: "AppStudentHomeworkAssignmentSubmissions",
                newName: "IX_AppStudentHomeworkAssignmentSubmissions_HomeworkAssignmentId");

            migrationBuilder.RenameIndex(
                name: "IX_HomeworkAssignments_HomeworkAssignmentTypeId",
                table: "AppHomeworkAssignments",
                newName: "IX_AppHomeworkAssignments_HomeworkAssignmentTypeId");

            migrationBuilder.AlterColumn<string>(
                name: "SubmittedDocumentUrl",
                table: "AppStudentHomeworkAssignmentSubmissions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Marks",
                table: "AppStudentHomeworkAssignmentSubmissions",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Feedback",
                table: "AppStudentHomeworkAssignmentSubmissions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "AppHomeworkAssignments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<bool>(
                name: "IsPublished",
                table: "AppHomeworkAssignments",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<Guid>(
                name: "HomeworkAssignmentTypeId",
                table: "AppHomeworkAssignments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DueDate",
                table: "AppHomeworkAssignments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "DivisionId",
                table: "AppHomeworkAssignments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppHomeworkAssignments_AppHomeworkAssignmentTypes_HomeworkA~",
                table: "AppHomeworkAssignments",
                column: "HomeworkAssignmentTypeId",
                principalTable: "AppHomeworkAssignmentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
