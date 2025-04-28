using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v19 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppStudentGroupMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsLeader = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId1 = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentGroupId1 = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentGroupMembers_AppStudentGroups_StudentGroupId",
                        column: x => x.StudentGroupId,
                        principalTable: "AppStudentGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppStudentGroupMembers_AppStudentGroups_StudentGroupId1",
                        column: x => x.StudentGroupId1,
                        principalTable: "AppStudentGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppStudentGroupMembers_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppStudentGroupMembers_AppStudents_StudentId1",
                        column: x => x.StudentId1,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentGroupMembers_StudentGroupId_IsLeader",
                table: "AppStudentGroupMembers",
                columns: new[] { "StudentGroupId", "IsLeader" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentGroupMembers_StudentGroupId1",
                table: "AppStudentGroupMembers",
                column: "StudentGroupId1");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentGroupMembers_StudentId_StudentGroupId",
                table: "AppStudentGroupMembers",
                columns: new[] { "StudentId", "StudentGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentGroupMembers_StudentId1",
                table: "AppStudentGroupMembers",
                column: "StudentId1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppStudentGroupMembers");
        }
    }
}
