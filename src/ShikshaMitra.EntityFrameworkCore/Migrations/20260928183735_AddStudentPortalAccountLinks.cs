using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentPortalAccountLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppStudentPortalAccountLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Relationship = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    VerifiedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AppStudentPortalAccountLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentPortalAccountLinks_AbpUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentPortalAccountLinks_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentPortalAccountLinks_StudentId",
                table: "AppStudentPortalAccountLinks",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentPortalAccountLinks_TenantId_StudentId_AccountType",
                table: "AppStudentPortalAccountLinks",
                columns: new[] { "TenantId", "StudentId", "AccountType" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentPortalAccountLinks_TenantId_UserId_AccountType",
                table: "AppStudentPortalAccountLinks",
                columns: new[] { "TenantId", "UserId", "AccountType" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentPortalAccountLinks_UserId",
                table: "AppStudentPortalAccountLinks",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppStudentPortalAccountLinks");
        }
    }
}
