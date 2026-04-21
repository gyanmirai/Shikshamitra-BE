using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentElectiveSelections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppStudentElectiveSelections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectiveGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedSubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentPreference = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_AppStudentElectiveSelections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentElectiveSelections_StudentId_ElectiveGroupId",
                table: "AppStudentElectiveSelections",
                columns: new[] { "StudentId", "ElectiveGroupId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppStudentElectiveSelections");
        }
    }
}
