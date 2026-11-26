using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class PersistHrSetupReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HR_SetupConfigurationRevisions",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_SetupConfigurationRevisions", x => x.TenantId);
                });

            migrationBuilder.CreateTable(
                name: "HR_SetupReadinessSnapshots",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<Guid>(type: "uuid", nullable: false),
                    IsReady = table.Column<bool>(type: "boolean", nullable: false),
                    CheckedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_SetupReadinessSnapshots", x => new { x.TenantId, x.AcademicYearId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HR_SetupConfigurationRevisions");

            migrationBuilder.DropTable(
                name: "HR_SetupReadinessSnapshots");
        }
    }
}
