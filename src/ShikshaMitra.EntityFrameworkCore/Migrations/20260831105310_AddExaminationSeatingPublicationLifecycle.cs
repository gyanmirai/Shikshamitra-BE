using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddExaminationSeatingPublicationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppExamSeatingPlanSubjects_ExamId_ExamSubjectId",
                table: "AppExamSeatingPlanSubjects");

            migrationBuilder.CreateTable(
                name: "AppExamSeatingPublications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AppExamSeatingPublications", x => x.Id);
                });

            migrationBuilder.Sql(
                """
                -- Remember examinations whose legacy subject ownership was ambiguous before
                -- cleanup. They must remain editable drafts and require an explicit new global
                -- publish action instead of being silently frozen by the legacy backfill.
                CREATE TEMP TABLE IF NOT EXISTS "__ExamSeatingDuplicateDrafts" (
                    "ExamId" uuid PRIMARY KEY
                );

                INSERT INTO "__ExamSeatingDuplicateDrafts" ("ExamId")
                SELECT plan_subject."ExamId"
                FROM "AppExamSeatingPlanSubjects" AS plan_subject
                JOIN "AppExamSeatingPlans" AS plan
                  ON plan."Id" = plan_subject."SeatingPlanId"
                 AND plan."IsDeleted" = FALSE
                GROUP BY plan_subject."ExamId", plan_subject."ExamSubjectId"
                HAVING COUNT(DISTINCT plan_subject."SeatingPlanId") > 1
                ON CONFLICT ("ExamId") DO NOTHING;

                -- Historical builds allowed the same scheduled subject to belong to multiple
                -- plans. Retain a populated plan (or a published plan when both are empty),
                -- but stop rather than guessing when two populated plans conflict.
                DELETE FROM "AppExamSeatingPlanSubjects" AS plan_subject
                USING "AppExamSeatingPlans" AS plan
                WHERE plan_subject."SeatingPlanId" = plan."Id"
                  AND plan."IsDeleted" = TRUE
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "AppExamSeatAllocations" AS allocation
                      WHERE allocation."SeatingPlanId" = plan."Id"
                        AND allocation."IsDeleted" = FALSE
                  );

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "AppExamSeatingPlanSubjects" AS plan_subject
                        JOIN "AppExamSeatingPlans" AS plan
                          ON plan."Id" = plan_subject."SeatingPlanId"
                         AND plan."IsDeleted" = FALSE
                        WHERE EXISTS (
                            SELECT 1
                            FROM "AppExamSeatAllocations" AS allocation
                            WHERE allocation."SeatingPlanId" = plan."Id"
                              AND allocation."IsDeleted" = FALSE
                        )
                        GROUP BY plan_subject."ExamId", plan_subject."ExamSubjectId"
                        HAVING COUNT(DISTINCT plan_subject."SeatingPlanId") > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot enforce one seating plan per exam subject: two populated plans own the same scheduled subject. Reset or repair the affected examination seating draft first.';
                    END IF;
                END $$;

                WITH plan_load AS (
                    SELECT plan."Id" AS "SeatingPlanId",
                           COUNT(allocation."Id") FILTER (WHERE allocation."IsDeleted" = FALSE) AS "AllocationCount"
                    FROM "AppExamSeatingPlans" AS plan
                    LEFT JOIN "AppExamSeatAllocations" AS allocation
                      ON allocation."SeatingPlanId" = plan."Id"
                    GROUP BY plan."Id"
                ), ranked_links AS (
                    SELECT plan_subject."Id",
                           COALESCE(plan_load."AllocationCount", 0) AS "AllocationCount",
                           ROW_NUMBER() OVER (
                               PARTITION BY plan_subject."ExamId", plan_subject."ExamSubjectId"
                               ORDER BY
                                   (plan."IsDeleted" = FALSE) DESC,
                                   (COALESCE(plan_load."AllocationCount", 0) > 0) DESC,
                                   (plan."Status" = 5) DESC,
                                   plan."CreationTime",
                                   plan_subject."Id"
                           ) AS "OwnershipRank"
                    FROM "AppExamSeatingPlanSubjects" AS plan_subject
                    JOIN "AppExamSeatingPlans" AS plan
                      ON plan."Id" = plan_subject."SeatingPlanId"
                    LEFT JOIN plan_load
                      ON plan_load."SeatingPlanId" = plan."Id"
                )
                DELETE FROM "AppExamSeatingPlanSubjects" AS plan_subject
                USING ranked_links
                WHERE plan_subject."Id" = ranked_links."Id"
                  AND ranked_links."OwnershipRank" > 1
                  AND ranked_links."AllocationCount" = 0;

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "AppExamSeatingPlanSubjects"
                        GROUP BY "ExamId", "ExamSubjectId"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot enforce one seating plan per exam subject because unresolved duplicate ownership remains.';
                    END IF;
                END $$;

                UPDATE "AppExamSeatingPlans" AS plan
                SET "IsDeleted" = TRUE,
                    "DeletionTime" = CURRENT_TIMESTAMP
                WHERE plan."IsDeleted" = FALSE
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "AppExamSeatingPlanSubjects" AS plan_subject
                      WHERE plan_subject."SeatingPlanId" = plan."Id"
                  )
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "AppExamSeatAllocations" AS allocation
                      WHERE allocation."SeatingPlanId" = plan."Id"
                        AND allocation."IsDeleted" = FALSE
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSeatingPlanSubjects_ExamId_ExamSubjectId",
                table: "AppExamSeatingPlanSubjects",
                columns: new[] { "ExamId", "ExamSubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSeatingPublications_ExamId",
                table: "AppExamSeatingPublications",
                column: "ExamId",
                unique: true);

            migrationBuilder.Sql(
                """
                -- Migrate only examinations that were already completely and consistently
                -- published by the legacy per-plan workflow. Partial legacy publication stays
                -- editable so the school can finish or reset it under the new lifecycle.
                INSERT INTO "AppExamSeatingPublications" (
                    "Id",
                    "TenantId",
                    "ExamId",
                    "PublishedAt",
                    "PublishedByUserId",
                    "ExtraProperties",
                    "ConcurrencyStamp",
                    "CreationTime",
                    "CreatorId",
                    "LastModificationTime",
                    "LastModifierId",
                    "IsDeleted",
                    "DeleterId",
                    "DeletionTime"
                )
                SELECT
                    (ARRAY_AGG(plan."Id" ORDER BY plan."Id"))[1],
                    (ARRAY_AGG(plan."TenantId" ORDER BY plan."Id"))[1],
                    plan."ExamId",
                    COALESCE(MAX(plan."PublishedAt"), CURRENT_TIMESTAMP),
                    COALESCE(
                        (ARRAY_AGG(plan."PublishedByUserId" ORDER BY plan."PublishedAt" DESC NULLS LAST)
                            FILTER (WHERE plan."PublishedByUserId" IS NOT NULL))[1],
                        '00000000-0000-0000-0000-000000000000'::uuid
                    ),
                    '{}',
                    MD5(plan."ExamId"::text || CURRENT_TIMESTAMP::text),
                    COALESCE(MAX(plan."PublishedAt"), CURRENT_TIMESTAMP),
                    NULL,
                    NULL,
                    NULL,
                    FALSE,
                    NULL,
                    NULL
                FROM "AppExamSeatingPlans" AS plan
                WHERE plan."IsDeleted" = FALSE
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "__ExamSeatingDuplicateDrafts" AS duplicate_draft
                      WHERE duplicate_draft."ExamId" = plan."ExamId"
                  )
                GROUP BY plan."ExamId"
                HAVING BOOL_AND(plan."Status" = 5)
                   AND NOT EXISTS (
                       SELECT 1
                       FROM "AppExamSubjects" AS exam_subject
                       WHERE exam_subject."ExamId" = plan."ExamId"
                         AND NOT EXISTS (
                             SELECT 1
                             FROM "AppExamSeatingPlanSubjects" AS plan_subject
                             JOIN "AppExamSeatingPlans" AS owning_plan
                               ON owning_plan."Id" = plan_subject."SeatingPlanId"
                              AND owning_plan."IsDeleted" = FALSE
                             WHERE plan_subject."ExamId" = plan."ExamId"
                               AND plan_subject."ExamSubjectId" = exam_subject."Id"
                         )
                   )
                   AND NOT EXISTS (
                       SELECT 1
                       FROM "AppExamSeatingPlans" AS empty_plan
                       WHERE empty_plan."ExamId" = plan."ExamId"
                         AND empty_plan."IsDeleted" = FALSE
                         AND NOT EXISTS (
                             SELECT 1
                             FROM "AppExamSeatingPlanSubjects" AS plan_subject
                             WHERE plan_subject."SeatingPlanId" = empty_plan."Id"
                         )
                   );

                -- The examination publication row is the only immutable boundary. Reopen every
                -- incomplete or historically ambiguous legacy examination so its plans can be
                -- regrouped, reset, and republished through the new single action.
                UPDATE "AppExamSeatingPlans" AS plan
                SET "IsLocked" = FALSE,
                    "Status" = CASE WHEN plan."Status" = 5 THEN 4 ELSE plan."Status" END,
                    "FinalizedAt" = NULL,
                    "FinalizedByUserId" = NULL,
                    "PublishedAt" = NULL,
                    "PublishedByUserId" = NULL
                WHERE plan."IsDeleted" = FALSE
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "AppExamSeatingPublications" AS publication
                      WHERE publication."ExamId" = plan."ExamId"
                        AND publication."IsDeleted" = FALSE
                  );

                DROP TABLE IF EXISTS "__ExamSeatingDuplicateDrafts";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppExamSeatingPublications");

            migrationBuilder.DropIndex(
                name: "IX_AppExamSeatingPlanSubjects_ExamId_ExamSubjectId",
                table: "AppExamSeatingPlanSubjects");

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSeatingPlanSubjects_ExamId_ExamSubjectId",
                table: "AppExamSeatingPlanSubjects",
                columns: new[] { "ExamId", "ExamSubjectId" });
        }
    }
}
