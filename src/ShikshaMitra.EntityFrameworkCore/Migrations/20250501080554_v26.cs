using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v26 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppSubjects_AppStandards_StandardId",
                table: "AppSubjects");

            migrationBuilder.DropForeignKey(
                name: "FK_AppSubjectTeacher_AppSubjects_SubjectId",
                table: "AppSubjectTeacher");

            migrationBuilder.DropIndex(
                name: "IX_AppSubjects_StandardId",
                table: "AppSubjects");

            migrationBuilder.DropColumn(
                name: "BoardId",
                table: "AppSubjects");

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

            migrationBuilder.DropColumn(
                name: "YearId",
                table: "AppSubjects");

            migrationBuilder.RenameColumn(
                name: "SubjectName",
                table: "AppSubjects",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "SubjectColor",
                table: "AppSubjects",
                newName: "Color");

            migrationBuilder.CreateTable(
                name: "TempSubject",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    YearId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediumId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TempSubject", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TempSubject_AppStandards_StandardId",
                        column: x => x.StandardId,
                        principalTable: "AppStandards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppSubjects_MediumId",
                table: "AppSubjects",
                column: "MediumId");

            migrationBuilder.CreateIndex(
                name: "IX_TempSubject_StandardId",
                table: "TempSubject",
                column: "StandardId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppSubjects_AppEducationMediums_MediumId",
                table: "AppSubjects",
                column: "MediumId",
                principalTable: "AppEducationMediums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppSubjectTeacher_TempSubject_SubjectId",
                table: "AppSubjectTeacher",
                column: "SubjectId",
                principalTable: "TempSubject",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppSubjects_AppEducationMediums_MediumId",
                table: "AppSubjects");

            migrationBuilder.DropForeignKey(
                name: "FK_AppSubjectTeacher_TempSubject_SubjectId",
                table: "AppSubjectTeacher");

            migrationBuilder.DropTable(
                name: "TempSubject");

            migrationBuilder.DropIndex(
                name: "IX_AppSubjects_MediumId",
                table: "AppSubjects");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "AppSubjects",
                newName: "SubjectName");

            migrationBuilder.RenameColumn(
                name: "Color",
                table: "AppSubjects",
                newName: "SubjectColor");

            migrationBuilder.AddColumn<Guid>(
                name: "BoardId",
                table: "AppSubjects",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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

            migrationBuilder.AddColumn<Guid>(
                name: "YearId",
                table: "AppSubjects",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_AppSubjects_StandardId",
                table: "AppSubjects",
                column: "StandardId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppSubjects_AppStandards_StandardId",
                table: "AppSubjects",
                column: "StandardId",
                principalTable: "AppStandards",
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
