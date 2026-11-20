using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class CompleteLibraryFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "AppBookCopies" c LEFT JOIN "AppBooks" b ON b."Id" = c."BookId"
                        WHERE b."Id" IS NULL OR length(trim(c."AccessionNumber")) = 0
                    ) THEN
                        RAISE EXCEPTION 'Library migration requires review of orphan copies or empty accession numbers.';
                    END IF;
                    IF EXISTS (
                        SELECT b."TenantId", upper(trim(c."AccessionNumber"))
                        FROM "AppBookCopies" c JOIN "AppBooks" b ON b."Id" = c."BookId"
                        GROUP BY b."TenantId", upper(trim(c."AccessionNumber")) HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Library migration requires review of duplicate normalized accessions within a school.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_AppLibraryNotificationSettings_EventType_RecipientRole_Chan~",
                table: "AppLibraryNotificationSettings");

            migrationBuilder.DropIndex(
                name: "IX_AppBookCopies_AccessionNumber",
                table: "AppBookCopies");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "AppLibraryNotificationHistories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedAt",
                table: "AppLibraryNotificationHistories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                table: "AppLibraryNotificationHistories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AppBookCopies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedAccessionNumber",
                table: "AppBookCopies",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                computedColumnSql: "upper(trim(\"AccessionNumber\"))",
                stored: true);

            migrationBuilder.Sql("""
                UPDATE "AppBookCopies" c SET "TenantId" = b."TenantId"
                FROM "AppBooks" b WHERE b."Id" = c."BookId";

                UPDATE "AppBooks" b SET "NumberOfCopies" = (
                    SELECT count(*)::integer FROM "AppBookCopies" c WHERE c."BookId" = b."Id"
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryNotificationSettings_TenantId_EventType_Recipient~",
                table: "AppLibraryNotificationSettings",
                columns: new[] { "TenantId", "EventType", "RecipientRole", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryNotificationHistories_Status_NextAttemptAt",
                table: "AppLibraryNotificationHistories",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryNotificationHistories_TenantId_DeduplicationKey",
                table: "AppLibraryNotificationHistories",
                columns: new[] { "TenantId", "DeduplicationKey" });

            migrationBuilder.CreateIndex(
                name: "IX_AppBookCopies_TenantId_NormalizedAccessionNumber",
                table: "AppBookCopies",
                columns: new[] { "TenantId", "NormalizedAccessionNumber" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT "AccessionNumber" FROM "AppBookCopies"
                        GROUP BY "AccessionNumber" HAVING count(*) > 1
                    ) OR EXISTS (
                        SELECT "EventType", "RecipientRole", "Channel" FROM "AppLibraryNotificationSettings"
                        GROUP BY "EventType", "RecipientRole", "Channel" HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Library rollback would restore global uniqueness and cannot preserve current school records. Use a forward repair.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_AppLibraryNotificationSettings_TenantId_EventType_Recipient~",
                table: "AppLibraryNotificationSettings");

            migrationBuilder.DropIndex(
                name: "IX_AppLibraryNotificationHistories_Status_NextAttemptAt",
                table: "AppLibraryNotificationHistories");

            migrationBuilder.DropIndex(
                name: "IX_AppLibraryNotificationHistories_TenantId_DeduplicationKey",
                table: "AppLibraryNotificationHistories");

            migrationBuilder.DropIndex(
                name: "IX_AppBookCopies_TenantId_NormalizedAccessionNumber",
                table: "AppBookCopies");

            migrationBuilder.DropColumn(
                name: "NormalizedAccessionNumber",
                table: "AppBookCopies");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "AppLibraryNotificationHistories");

            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                table: "AppLibraryNotificationHistories");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "AppLibraryNotificationHistories");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AppBookCopies");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryNotificationSettings_EventType_RecipientRole_Chan~",
                table: "AppLibraryNotificationSettings",
                columns: new[] { "EventType", "RecipientRole", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppBookCopies_AccessionNumber",
                table: "AppBookCopies",
                column: "AccessionNumber",
                unique: true);
        }
    }
}
