using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentOnboardingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppStudentOnboardingSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_AppStudentOnboardingSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentOnboardingSettings_academic_Years_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "academic_Years",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingSettings_AcademicYearId",
                table: "AppStudentOnboardingSettings",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingSettings_AcademicYear",
                table: "AppStudentOnboardingSettings",
                columns: new[] { "TenantId", "AcademicYearId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"AcademicYearId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingSettings_SchoolDefault",
                table: "AppStudentOnboardingSettings",
                column: "TenantId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"AcademicYearId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppStudentOnboardingSettings");
        }
    }
}
