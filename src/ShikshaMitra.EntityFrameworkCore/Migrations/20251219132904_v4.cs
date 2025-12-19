using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppTeacherSubjectAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AppTeacherSubjectAllocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAllocatedTeachers",
                columns: table => new
                {
                    TeacherSubjectAllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAllocatedTeachers", x => new { x.TeacherSubjectAllocationId, x.TeacherId });
                    table.ForeignKey(
                        name: "FK_AppAllocatedTeachers_AppTeacherSubjectAllocations_TeacherSu~",
                        column: x => x.TeacherSubjectAllocationId,
                        principalTable: "AppTeacherSubjectAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAllocatedTeachers_TeacherId",
                table: "AppAllocatedTeachers",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeacherSubjectAllocations_DivisionId",
                table: "AppTeacherSubjectAllocations",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeacherSubjectAllocations_StandardId",
                table: "AppTeacherSubjectAllocations",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeacherSubjectAllocations_StandardId_DivisionId_SubjectId",
                table: "AppTeacherSubjectAllocations",
                columns: new[] { "StandardId", "DivisionId", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTeacherSubjectAllocations_SubjectId",
                table: "AppTeacherSubjectAllocations",
                column: "SubjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAllocatedTeachers");

            migrationBuilder.DropTable(
                name: "AppTeacherSubjectAllocations");
        }
    }
}
