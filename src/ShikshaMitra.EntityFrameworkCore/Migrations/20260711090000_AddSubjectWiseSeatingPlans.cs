using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ShikshaMitraDbContext))]
    [Migration("20260711090000_AddSubjectWiseSeatingPlans")]
    public partial class AddSubjectWiseSeatingPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS "AppExamSeatingPlans"
                    ADD COLUMN IF NOT EXISTS "Name" character varying(160) NOT NULL DEFAULT 'All Subjects',
                    ADD COLUMN IF NOT EXISTS "ScopeType" integer NOT NULL DEFAULT 0;

                ALTER TABLE IF EXISTS "AppExamSeatAllocations"
                    ADD COLUMN IF NOT EXISTS "SeatingPlanId" uuid;
                """);

            migrationBuilder.CreateTable(
                name: "AppExamSeatingPlanSubjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeatingPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamSubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppExamSeatingPlanSubjects", x => x.Id);
                });

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('public."AppExams"') IS NOT NULL
                       AND to_regclass('public."AppExamSeatAllocations"') IS NOT NULL
                       AND to_regclass('public."AppExamSeatingPlans"') IS NOT NULL THEN
                        INSERT INTO "AppExamSeatingPlans" (
                            "Id", "TenantId", "ExamId", "Name", "ScopeType", "Status", "RoomIdsCsv",
                            "AllocationRulesJson", "ExcludedStudentIdsCsv", "SpecialRequirementStudentIdsCsv",
                            "IsLocked", "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted"
                        )
                        SELECT
                            (md5(random()::text || clock_timestamp()::text))::uuid,
                            e."TenantId",
                            e."Id",
                            'All Subjects',
                            0,
                            0,
                            '',
                            '',
                            '',
                            '',
                            false,
                            '{}',
                            md5(random()::text || clock_timestamp()::text),
                            NOW(),
                            false
                        FROM "AppExams" e
                        WHERE EXISTS (
                            SELECT 1 FROM "AppExamSeatAllocations" a WHERE a."ExamId" = e."Id"
                        )
                        AND NOT EXISTS (
                            SELECT 1 FROM "AppExamSeatingPlans" p WHERE p."ExamId" = e."Id"
                        );
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('public."AppExamSeatAllocations"') IS NOT NULL
                       AND to_regclass('public."AppExamSeatingPlans"') IS NOT NULL THEN
                        UPDATE "AppExamSeatAllocations" a
                        SET "SeatingPlanId" = p."Id"
                        FROM "AppExamSeatingPlans" p
                        WHERE p."ExamId" = a."ExamId"
                          AND p."ScopeType" = 0
                          AND a."SeatingPlanId" IS NULL;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('public."AppExamSeatingPlanSubjects"') IS NOT NULL
                       AND to_regclass('public."AppExamSeatingPlans"') IS NOT NULL
                       AND to_regclass('public."AppExamSubjects"') IS NOT NULL THEN
                        INSERT INTO "AppExamSeatingPlanSubjects" ("Id", "TenantId", "SeatingPlanId", "ExamId", "ExamSubjectId", "SubjectId")
                        SELECT (md5(random()::text || clock_timestamp()::text))::uuid, p."TenantId", p."Id", es."ExamId", es."Id", es."SubjectId"
                        FROM "AppExamSeatingPlans" p
                        INNER JOIN "AppExamSubjects" es ON es."ExamId" = p."ExamId"
                        WHERE p."ScopeType" = 0
                          AND NOT EXISTS (
                              SELECT 1 FROM "AppExamSeatingPlanSubjects" ps
                              WHERE ps."SeatingPlanId" = p."Id" AND ps."ExamSubjectId" = es."Id"
                          );
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS "AppExamSeatAllocations"
                    ALTER COLUMN "SeatingPlanId" SET NOT NULL;

                DROP INDEX IF EXISTS "IX_AppExamSeatingPlans_ExamId";
                DROP INDEX IF EXISTS "IX_AppExamSeatAllocations_ExamId_StudentId";

                DO $$
                BEGIN
                    IF to_regclass('public."AppExamSeatingPlans"') IS NOT NULL THEN
                        CREATE INDEX IF NOT EXISTS "IX_AppExamSeatingPlans_ExamId"
                            ON "AppExamSeatingPlans" ("ExamId");
                    END IF;

                    IF to_regclass('public."AppExamSeatingPlanSubjects"') IS NOT NULL THEN
                        CREATE INDEX IF NOT EXISTS "IX_AppExamSeatingPlanSubjects_ExamId_ExamSubjectId"
                            ON "AppExamSeatingPlanSubjects" ("ExamId", "ExamSubjectId");
                        CREATE UNIQUE INDEX IF NOT EXISTS "IX_AppExamSeatingPlanSubjects_SeatingPlanId_ExamSubjectId"
                            ON "AppExamSeatingPlanSubjects" ("SeatingPlanId", "ExamSubjectId");
                    END IF;

                    IF to_regclass('public."AppExamSeatAllocations"') IS NOT NULL THEN
                        CREATE INDEX IF NOT EXISTS "IX_AppExamSeatAllocations_ExamId"
                            ON "AppExamSeatAllocations" ("ExamId");
                        CREATE UNIQUE INDEX IF NOT EXISTS "IX_AppExamSeatAllocations_SeatingPlanId_RoomId_SeatNumber"
                            ON "AppExamSeatAllocations" ("SeatingPlanId", "RoomId", "SeatNumber");
                        CREATE UNIQUE INDEX IF NOT EXISTS "IX_AppExamSeatAllocations_SeatingPlanId_StudentId"
                            ON "AppExamSeatAllocations" ("SeatingPlanId", "StudentId");
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppExamSeatAllocations_SeatingPlanId_StudentId",
                table: "AppExamSeatAllocations");

            migrationBuilder.DropIndex(
                name: "IX_AppExamSeatAllocations_SeatingPlanId_RoomId_SeatNumber",
                table: "AppExamSeatAllocations");

            migrationBuilder.DropIndex(
                name: "IX_AppExamSeatAllocations_ExamId",
                table: "AppExamSeatAllocations");

            migrationBuilder.DropTable(
                name: "AppExamSeatingPlanSubjects");

            migrationBuilder.DropIndex(
                name: "IX_AppExamSeatingPlans_ExamId",
                table: "AppExamSeatingPlans");

            migrationBuilder.DropColumn(
                name: "SeatingPlanId",
                table: "AppExamSeatAllocations");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "AppExamSeatingPlans");

            migrationBuilder.DropColumn(
                name: "ScopeType",
                table: "AppExamSeatingPlans");

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSeatingPlans_ExamId",
                table: "AppExamSeatingPlans",
                column: "ExamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSeatAllocations_ExamId_StudentId",
                table: "AppExamSeatAllocations",
                columns: new[] { "ExamId", "StudentId" },
                unique: true);
        }
    }
}
