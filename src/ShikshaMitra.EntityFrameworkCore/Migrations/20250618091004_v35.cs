using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v35 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppSubjects_AppEducationMediums_AcademicMediumId",
                table: "AppSubjects");

            migrationBuilder.RenameColumn(
                name: "AcademicMediumId",
                table: "AppSubjects",
                newName: "EducationMediumId");

            migrationBuilder.RenameIndex(
                name: "IX_AppSubjects_AcademicMediumId",
                table: "AppSubjects",
                newName: "IX_AppSubjects_EducationMediumId");

            migrationBuilder.CreateTable(
                name: "AppSubjectChapters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterNumber = table.Column<int>(type: "integer", nullable: false),
                    ChapterName = table.Column<string>(type: "text", nullable: false),
                    ChapterStartTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChapterEndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_AppSubjectChapters", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_AppSubjects_AppEducationMediums_EducationMediumId",
                table: "AppSubjects",
                column: "EducationMediumId",
                principalTable: "AppEducationMediums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppSubjects_AppEducationMediums_EducationMediumId",
                table: "AppSubjects");

            migrationBuilder.DropTable(
                name: "AppSubjectChapters");

            migrationBuilder.RenameColumn(
                name: "EducationMediumId",
                table: "AppSubjects",
                newName: "AcademicMediumId");

            migrationBuilder.RenameIndex(
                name: "IX_AppSubjects_EducationMediumId",
                table: "AppSubjects",
                newName: "IX_AppSubjects_AcademicMediumId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppSubjects_AppEducationMediums_AcademicMediumId",
                table: "AppSubjects",
                column: "AcademicMediumId",
                principalTable: "AppEducationMediums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
