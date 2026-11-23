using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentAppAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppStudentAppAccessEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Relationship = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentAppAccessEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentAppAccessEvents_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentAppAccessRestrictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsOff = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentAppAccessRestrictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentAppAccessRestrictions_AbpUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentAppAccessRestrictions_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAppAccessEvents_StudentId",
                table: "AppStudentAppAccessEvents",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAppAccessEvents_TenantId_StudentId_ChangedAt",
                table: "AppStudentAppAccessEvents",
                columns: new[] { "TenantId", "StudentId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAppAccessRestrictions_StudentId",
                table: "AppStudentAppAccessRestrictions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAppAccessRestrictions_TenantId_StudentId",
                table: "AppStudentAppAccessRestrictions",
                columns: new[] { "TenantId", "StudentId" },
                unique: true,
                filter: "\"UserId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAppAccessRestrictions_TenantId_StudentId_UserId",
                table: "AppStudentAppAccessRestrictions",
                columns: new[] { "TenantId", "StudentId", "UserId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAppAccessRestrictions_TenantId_UserId",
                table: "AppStudentAppAccessRestrictions",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAppAccessRestrictions_UserId",
                table: "AppStudentAppAccessRestrictions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppStudentAppAccessEvents");

            migrationBuilder.DropTable(
                name: "AppStudentAppAccessRestrictions");
        }
    }
}
