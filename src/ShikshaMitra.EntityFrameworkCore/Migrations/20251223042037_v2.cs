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
                name: "FK_AppChapterDocuments_AppSubjectChapters_ChapterId",
                table: "AppChapterDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppElectiveGroups_AppEducationMediums_EducationMediumId",
                table: "AppElectiveGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_AppSubjectTeachers_AppStandardSubjectAssignments_StandardSu~",
                table: "AppSubjectTeachers");

            migrationBuilder.DropIndex(
                name: "IX_AppSubjectTeachers_StandardSubjectAssignmentId",
                table: "AppSubjectTeachers");

            migrationBuilder.DropIndex(
                name: "IX_SubjectChapters_EducationMediumId",
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

            migrationBuilder.DropIndex(
                name: "IX_ChapterDocuments_ChapterId",
                table: "AppChapterDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ChapterDocuments_TenantId",
                table: "AppChapterDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ChapterDocuments_TenantId_ChapterId",
                table: "AppChapterDocuments");

            migrationBuilder.DropColumn(
                name: "StandardSubjectAssignmentId",
                table: "AppSubjectTeachers");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "EducationMediumId",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "ExtraProperties",
                table: "AppSubjectChapters");

            migrationBuilder.DropColumn(
                name: "StandardId",
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
                name: "SubjectColor",
                table: "AppStandardSubjectAssignments");

            migrationBuilder.DropColumn(
                name: "UploadedDocument",
                table: "AppStandardSubjectAssignments");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "AppChapterDocuments");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "AppChapterDocuments");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "AppChapterDocuments");

            migrationBuilder.DropColumn(
                name: "FileUrl",
                table: "AppChapterDocuments");

            migrationBuilder.RenameColumn(
                name: "SubjectId",
                table: "AppSubjectTeachers",
                newName: "AssignedSubjectId");

            migrationBuilder.RenameColumn(
                name: "SubjectId",
                table: "AppSubjectChapters",
                newName: "AssignedSubjectId");

            migrationBuilder.RenameIndex(
                name: "IX_SubjectChapters_SubjectId",
                table: "AppSubjectChapters",
                newName: "IX_AppSubjectChapters_AssignedSubjectId");

            migrationBuilder.RenameColumn(
                name: "EducationMediumId",
                table: "AppElectiveGroups",
                newName: "AcademicMediumId");

            migrationBuilder.RenameIndex(
                name: "IX_AppElectiveGroups_EducationMediumId",
                table: "AppElectiveGroups",
                newName: "IX_AppElectiveGroups_AcademicMediumId");

            migrationBuilder.AlterColumn<Guid>(
                name: "ChapterId",
                table: "AppChapterDocuments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedSubjectChapterId",
                table: "AppChapterDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedSubjectId",
                table: "AppChapterDocuments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "BlobName",
                table: "AppChapterDocuments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreationTime",
                table: "AppChapterDocuments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatorId",
                table: "AppChapterDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeleterId",
                table: "AppChapterDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionTime",
                table: "AppChapterDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AppChapterDocuments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModificationTime",
                table: "AppChapterDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifierId",
                table: "AppChapterDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStandardSubjectAssignments_StandardId",
                table: "AppStandardSubjectAssignments",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppChapterDocuments_AssignedSubjectChapterId",
                table: "AppChapterDocuments",
                column: "AssignedSubjectChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppChapterDocuments_AssignedSubjectId",
                table: "AppChapterDocuments",
                column: "AssignedSubjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppChapterDocuments_AppStandardSubjectAssignments_AssignedS~",
                table: "AppChapterDocuments",
                column: "AssignedSubjectId",
                principalTable: "AppStandardSubjectAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppChapterDocuments_AppSubjectChapters_AssignedSubjectChapt~",
                table: "AppChapterDocuments",
                column: "AssignedSubjectChapterId",
                principalTable: "AppSubjectChapters",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppElectiveGroups_AppEducationMediums_AcademicMediumId",
                table: "AppElectiveGroups",
                column: "AcademicMediumId",
                principalTable: "AppEducationMediums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppSubjectChapters_AppStandardSubjectAssignments_AssignedSu~",
                table: "AppSubjectChapters",
                column: "AssignedSubjectId",
                principalTable: "AppStandardSubjectAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppSubjectTeachers_AppStandardSubjectAssignments_AssignedSu~",
                table: "AppSubjectTeachers",
                column: "AssignedSubjectId",
                principalTable: "AppStandardSubjectAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppChapterDocuments_AppStandardSubjectAssignments_AssignedS~",
                table: "AppChapterDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppChapterDocuments_AppSubjectChapters_AssignedSubjectChapt~",
                table: "AppChapterDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppElectiveGroups_AppEducationMediums_AcademicMediumId",
                table: "AppElectiveGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_AppSubjectChapters_AppStandardSubjectAssignments_AssignedSu~",
                table: "AppSubjectChapters");

            migrationBuilder.DropForeignKey(
                name: "FK_AppSubjectTeachers_AppStandardSubjectAssignments_AssignedSu~",
                table: "AppSubjectTeachers");

            migrationBuilder.DropIndex(
                name: "IX_AppStandardSubjectAssignments_StandardId",
                table: "AppStandardSubjectAssignments");

            migrationBuilder.DropIndex(
                name: "IX_AppChapterDocuments_AssignedSubjectChapterId",
                table: "AppChapterDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AppChapterDocuments_AssignedSubjectId",
                table: "AppChapterDocuments");

            migrationBuilder.DropColumn(
                name: "AssignedSubjectChapterId",
                table: "AppChapterDocuments");

            migrationBuilder.DropColumn(
                name: "AssignedSubjectId",
                table: "AppChapterDocuments");

            migrationBuilder.DropColumn(
                name: "BlobName",
                table: "AppChapterDocuments");

            migrationBuilder.DropColumn(
                name: "CreationTime",
                table: "AppChapterDocuments");

            migrationBuilder.DropColumn(
                name: "CreatorId",
                table: "AppChapterDocuments");

            migrationBuilder.DropColumn(
                name: "DeleterId",
                table: "AppChapterDocuments");

            migrationBuilder.DropColumn(
                name: "DeletionTime",
                table: "AppChapterDocuments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AppChapterDocuments");

            migrationBuilder.DropColumn(
                name: "LastModificationTime",
                table: "AppChapterDocuments");

            migrationBuilder.DropColumn(
                name: "LastModifierId",
                table: "AppChapterDocuments");

            migrationBuilder.RenameColumn(
                name: "AssignedSubjectId",
                table: "AppSubjectTeachers",
                newName: "SubjectId");

            migrationBuilder.RenameColumn(
                name: "AssignedSubjectId",
                table: "AppSubjectChapters",
                newName: "SubjectId");

            migrationBuilder.RenameIndex(
                name: "IX_AppSubjectChapters_AssignedSubjectId",
                table: "AppSubjectChapters",
                newName: "IX_SubjectChapters_SubjectId");

            migrationBuilder.RenameColumn(
                name: "AcademicMediumId",
                table: "AppElectiveGroups",
                newName: "EducationMediumId");

            migrationBuilder.RenameIndex(
                name: "IX_AppElectiveGroups_AcademicMediumId",
                table: "AppElectiveGroups",
                newName: "IX_AppElectiveGroups_EducationMediumId");

            migrationBuilder.AddColumn<Guid>(
                name: "StandardSubjectAssignmentId",
                table: "AppSubjectTeachers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyStamp",
                table: "AppSubjectChapters",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "EducationMediumId",
                table: "AppSubjectChapters",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "ExtraProperties",
                table: "AppSubjectChapters",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "StandardId",
                table: "AppSubjectChapters",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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

            migrationBuilder.AddColumn<string>(
                name: "SubjectColor",
                table: "AppStandardSubjectAssignments",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UploadedDocument",
                table: "AppStandardSubjectAssignments",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ChapterId",
                table: "AppChapterDocuments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "AppChapterDocuments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "AppChapterDocuments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "AppChapterDocuments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "FileUrl",
                table: "AppChapterDocuments",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AppSubjectTeachers_StandardSubjectAssignmentId",
                table: "AppSubjectTeachers",
                column: "StandardSubjectAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectChapters_EducationMediumId",
                table: "AppSubjectChapters",
                column: "EducationMediumId");

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

            migrationBuilder.CreateIndex(
                name: "IX_ChapterDocuments_TenantId",
                table: "AppChapterDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ChapterDocuments_TenantId_ChapterId",
                table: "AppChapterDocuments",
                columns: new[] { "TenantId", "ChapterId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AppChapterDocuments_AppSubjectChapters_ChapterId",
                table: "AppChapterDocuments",
                column: "ChapterId",
                principalTable: "AppSubjectChapters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppElectiveGroups_AppEducationMediums_EducationMediumId",
                table: "AppElectiveGroups",
                column: "EducationMediumId",
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
    }
}
