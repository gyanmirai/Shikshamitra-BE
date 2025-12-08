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
            migrationBuilder.DropIndex(
                name: "IX_AppStandardSubjectAssignments_StandardId",
                table: "AppStandardSubjectAssignments");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "AppSubjectChapters",
                type: "character varying(5000)",
                maxLength: 5000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ChapterName",
                table: "AppSubjectChapters",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<Guid>(
                name: "EducationMediumId",
                table: "AppSubjectChapters",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<string>(
                name: "SubjectCode",
                table: "AppStandardSubjectAssignments",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<Guid>(
                name: "AssignmentGroupId",
                table: "AppStandardSubjectAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignmentGroupName",
                table: "AppStandardSubjectAssignments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Documents",
                table: "AppStandardSubjectAssignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EducationMediumId",
                table: "AppStandardSubjectAssignments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsSubjectOpen",
                table: "AppStandardSubjectAssignments",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "AppStandardSubjectAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubjectColor",
                table: "AppStandardSubjectAssignments",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubjectType",
                table: "AppStandardSubjectAssignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AppChapterDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FileUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppChapterDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppChapterDocuments_AppSubjectChapters_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "AppSubjectChapters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubjectChapters_EducationMediumId",
                table: "AppSubjectChapters",
                column: "EducationMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectChapters_SubjectId",
                table: "AppSubjectChapters",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectChapters_SubjectId_ChapterNumber",
                table: "AppSubjectChapters",
                columns: new[] { "SubjectId", "ChapterNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StandardSubjectAssignments_AssignmentGroupId",
                table: "AppStandardSubjectAssignments",
                column: "AssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_StandardSubjectAssignments_EducationMediumId",
                table: "AppStandardSubjectAssignments",
                column: "EducationMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_StandardSubjectAssignments_StandardId_SubjectCode",
                table: "AppStandardSubjectAssignments",
                columns: new[] { "StandardId", "SubjectCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChapterDocuments_ChapterId",
                table: "AppChapterDocuments",
                column: "ChapterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppChapterDocuments");

            migrationBuilder.DropIndex(
                name: "IX_SubjectChapters_EducationMediumId",
                table: "AppSubjectChapters");

            migrationBuilder.DropIndex(
                name: "IX_SubjectChapters_SubjectId",
                table: "AppSubjectChapters");

            migrationBuilder.DropIndex(
                name: "IX_SubjectChapters_SubjectId_ChapterNumber",
                table: "AppSubjectChapters");

            migrationBuilder.DropIndex(
                name: "IX_StandardSubjectAssignments_AssignmentGroupId",
                table: "AppStandardSubjectAssignments");

            migrationBuilder.DropIndex(
                name: "IX_StandardSubjectAssignments_EducationMediumId",
                table: "AppStandardSubjectAssignments");

            migrationBuilder.DropIndex(
                name: "IX_StandardSubjectAssignments_StandardId_SubjectCode",
                table: "AppStandardSubjectAssignments");

            migrationBuilder.DropColumn(
                name: "EducationMediumId",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "AssignmentGroupId",
                table: "AppStandardSubjectAssignments");

            migrationBuilder.DropColumn(
                name: "AssignmentGroupName",
                table: "AppStandardSubjectAssignments");

            migrationBuilder.DropColumn(
                name: "Documents",
                table: "AppStandardSubjectAssignments");

            migrationBuilder.DropColumn(
                name: "EducationMediumId",
                table: "AppStandardSubjectAssignments");

            migrationBuilder.DropColumn(
                name: "IsSubjectOpen",
                table: "AppStandardSubjectAssignments");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "AppStandardSubjectAssignments");

            migrationBuilder.DropColumn(
                name: "SubjectColor",
                table: "AppStandardSubjectAssignments");

            migrationBuilder.DropColumn(
                name: "SubjectType",
                table: "AppStandardSubjectAssignments");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "AppSubjectChapters",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(5000)",
                oldMaxLength: 5000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ChapterName",
                table: "AppSubjectChapters",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "SubjectCode",
                table: "AppStandardSubjectAssignments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.CreateIndex(
                name: "IX_AppStandardSubjectAssignments_StandardId",
                table: "AppStandardSubjectAssignments",
                column: "StandardId");
        }
    }
}
