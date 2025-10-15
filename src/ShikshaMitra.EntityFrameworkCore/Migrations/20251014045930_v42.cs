using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v42 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppGradeScales_AppEducationMediums_EducationMediumId",
                table: "AppGradeScales");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "AppGradeScales",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "AppChapters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AppChapters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppChapters_AppStandards_StandardId",
                        column: x => x.StandardId,
                        principalTable: "AppStandards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppChapters_AppSubjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "AppSubjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppChapterDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Base64Content = table.Column<string>(type: "text", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppChapterDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppChapterDocuments_AppChapters_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "AppChapters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppGradeScales_Title_EducationMediumId",
                table: "AppGradeScales",
                columns: new[] { "Title", "EducationMediumId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppChapterDocuments_ChapterId",
                table: "AppChapterDocuments",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppChapters_StandardId",
                table: "AppChapters",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppChapters_SubjectId",
                table: "AppChapters",
                column: "SubjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppGradeScales_AppEducationMediums_EducationMediumId",
                table: "AppGradeScales",
                column: "EducationMediumId",
                principalTable: "AppEducationMediums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppGradeScales_AppEducationMediums_EducationMediumId",
                table: "AppGradeScales");

            migrationBuilder.DropTable(
                name: "AppChapterDocuments");

            migrationBuilder.DropTable(
                name: "AppChapters");

            migrationBuilder.DropIndex(
                name: "IX_AppGradeScales_Title_EducationMediumId",
                table: "AppGradeScales");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "AppGradeScales",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AddForeignKey(
                name: "FK_AppGradeScales_AppEducationMediums_EducationMediumId",
                table: "AppGradeScales",
                column: "EducationMediumId",
                principalTable: "AppEducationMediums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
