using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddInstituteSetupProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppInstituteSetupProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentStep = table.Column<int>(type: "integer", nullable: false),
                    FurthestStep = table.Column<int>(type: "integer", nullable: false),
                    DraftJson = table.Column<string>(type: "character varying(16384)", maxLength: 16384, nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AppInstituteSetupProgress", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppInstituteSetupProgress_TenantId",
                table: "AppInstituteSetupProgress",
                column: "TenantId",
                unique: true);

            // Existing completion markers remain authoritative. Operational schools without a
            // legacy marker are preserved only when a complete academic branch already exists.
            migrationBuilder.Sql("""
                WITH legacy AS (
                    SELECT "TenantId", MAX("State") AS state
                    FROM "academic_InstituteSetupExtraProperties" GROUP BY "TenantId"
                ), classified AS (
                    SELECT t."Id", l.state,
                        l.state = 3 OR (l.state IS NULL AND EXISTS (
                            SELECT 1 FROM "academic_Divisions" d
                            JOIN "academic_Standards" s ON s."Id" = d."StandardId" AND s."TenantId" = t."Id" AND NOT s."IsDeleted"
                            JOIN "academic_EducationMediumStages" st ON st."Id" = s."EducationMediumStageId" AND st."TenantId" = t."Id" AND NOT st."IsDeleted"
                            JOIN "academic_EducationMediums" m ON m."Id" = st."EducationMediumId" AND m."TenantId" = t."Id" AND NOT m."IsDeleted"
                            JOIN "academic_EducationBoards" b ON b."Id" = m."EducationBoardId" AND b."TenantId" = t."Id" AND NOT b."IsDeleted"
                            JOIN "academic_Years" y ON y."Id" = b."AcademicYearId" AND y."TenantId" = t."Id" AND NOT y."IsDeleted"
                            WHERE d."TenantId" = t."Id" AND NOT d."IsDeleted" AND d."Strength" > 0 AND y."EndDate" > y."StartDate"
                        )) AS completed
                    FROM "AbpTenants" t LEFT JOIN legacy l ON l."TenantId" = t."Id"
                    WHERE NOT t."IsDeleted"
                )
                INSERT INTO "AppInstituteSetupProgress"
                    ("Id", "TenantId", "CurrentStep", "FurthestStep", "DraftJson", "CompletedAt", "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted")
                SELECT "Id", "Id", CASE WHEN completed THEN 6 ELSE COALESCE(state, 0) END,
                    CASE WHEN completed THEN 6 ELSE COALESCE(state, 0) END, '{}',
                    CASE WHEN completed THEN CURRENT_TIMESTAMP ELSE NULL END, '{}', md5("Id"::text), CURRENT_TIMESTAMP, false
                FROM classified;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppInstituteSetupProgress");
        }
    }
}
