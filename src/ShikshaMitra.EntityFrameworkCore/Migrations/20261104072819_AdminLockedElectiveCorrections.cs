using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AdminLockedElectiveCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyStamp",
                table: "academic_StudentElectiveSelections",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "PreferenceOverrideSubjectId",
                table: "academic_StudentElectiveSelections",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "academic_StudentElectiveCorrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectiveGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeforeJson = table.Column<string>(type: "text", nullable: false),
                    AfterJson = table.Column<string>(type: "text", nullable: false),
                    ImpactsJson = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academic_StudentElectiveCorrections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_academic_StudentElectiveCorrections_TenantId_StudentId_Elec~",
                table: "academic_StudentElectiveCorrections",
                columns: new[] { "TenantId", "StudentId", "ElectiveGroupId", "CreationTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "academic_StudentElectiveCorrections");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "academic_StudentElectiveSelections");

            migrationBuilder.DropColumn(
                name: "PreferenceOverrideSubjectId",
                table: "academic_StudentElectiveSelections");
        }
    }
}
