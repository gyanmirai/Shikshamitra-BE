using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    public partial class AddStudentIdCardAssignments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppStudentIdCardAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AppStudentIdCardAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentIdCardAssignments_Templates",
                        column: x => x.TemplateId,
                        principalTable: "AppStudentIdCardTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentIdCardAssignments_TemplateId",
                table: "AppStudentIdCardAssignments",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentIdCardAssignments_TenantTemplate",
                table: "AppStudentIdCardAssignments",
                columns: new[] { "TenantId", "TemplateId" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_StudentIdCardAssignments_TenantYearClass",
                table: "AppStudentIdCardAssignments",
                columns: new[] { "TenantId", "AcademicYearId", "StandardId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AppStudentIdCardAssignments");
        }
    }
}
