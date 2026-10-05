using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations;

/// <summary>
/// Performance criteria derive their limit from the highest shared level score.
/// Existing points/weights are copied without rounding; criterion IDs and awarded scores remain unchanged.
/// Original performance caps and totals are retained in rubric ExtraProperties for an exact rollback.
/// Invalid legacy level JSON aborts the transaction rather than silently losing scoring data.
/// </summary>
public partial class RemoveRubricCriterionMaxMarks : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<decimal>("Points", "academic_AssessmentRubricCriteria", "numeric(10,2)", nullable: true, precision: 10, scale: 2);
		migrationBuilder.AddColumn<decimal>("Weight", "academic_AssessmentRubricCriteria", "numeric(10,2)", nullable: true, precision: 10, scale: 2);
		migrationBuilder.Sql("""
			UPDATE "academic_AssessmentRubricCriteria" c
			SET "Points" = CASE WHEN r."ScoringModel" NOT IN (1, 2) THEN c."MaxMarks" END,
			    "Weight" = CASE WHEN r."ScoringModel" = 1 THEN c."MaxMarks" END
			FROM "academic_AssessmentRubrics" r WHERE r."Id" = c."RubricId";

			UPDATE "academic_AssessmentRubrics" r
			SET "ExtraProperties" = (COALESCE(NULLIF(r."ExtraProperties", ''), '{}')::jsonb ||
			    jsonb_build_object('LegacyCriterionMaxMarks', (SELECT jsonb_object_agg(c."Id"::text, c."MaxMarks")
			      FROM "academic_AssessmentRubricCriteria" c WHERE c."RubricId" = r."Id"),
			      'LegacyRubricTotalMarks', r."TotalMarks", 'LegacyPerformanceLevelsJson', r."PerformanceLevelsJson"))::text
			WHERE r."ScoringModel" = 2;

			-- Older draft APIs allowed missing levels. Recover a shared scale from their
			-- stored caps, including zero, so these drafts still open and can be completed.
			UPDATE "academic_AssessmentRubrics" r SET "PerformanceLevelsJson" = (
			  SELECT jsonb_agg(jsonb_build_object('Level', numbered.n, 'Label', 'Score ' || numbered.score,
			    'Score', numbered.score, 'StudentLabel', 'Score ' || numbered.score) ORDER BY numbered.score)::text
			  FROM (SELECT score, row_number() OVER (ORDER BY score) AS n FROM (
			    SELECT DISTINCT c."MaxMarks" AS score FROM "academic_AssessmentRubricCriteria" c WHERE c."RubricId" = r."Id" AND NOT c."IsDeleted"
			    UNION SELECT 0::numeric) scores) numbered)
			WHERE r."ScoringModel" = 2 AND COALESCE((SELECT MAX(COALESCE(l->>'Score', l->>'score')::numeric)
			  FROM jsonb_array_elements(COALESCE(NULLIF(r."PerformanceLevelsJson", ''), '[]')::jsonb) l), 0) <= 0;

			DO $$ BEGIN
			  IF EXISTS (SELECT 1 FROM "academic_AssessmentRubrics" r WHERE r."ScoringModel" = 2
			    AND COALESCE((SELECT MAX(COALESCE(l->>'Score', l->>'score')::numeric)
			      FROM jsonb_array_elements(COALESCE(NULLIF(r."PerformanceLevelsJson", ''), '[]')::jsonb) l), 0) <= 0) THEN
			    RAISE EXCEPTION 'Performance rubric has no positive level score; repair its levels before migration.';
			  END IF;
			END $$;

			UPDATE "academic_AssessmentRubrics" r
			SET "TotalMarks" = (SELECT MAX(COALESCE(l->>'Score', l->>'score')::numeric)
			    FROM jsonb_array_elements(r."PerformanceLevelsJson"::jsonb) l) *
			    (SELECT COUNT(*) FROM "academic_AssessmentRubricCriteria" c WHERE c."RubricId" = r."Id" AND NOT c."IsDeleted")
			WHERE r."ScoringModel" = 2;

			-- Approved evaluation feedback stores the denominator on the submission for reports.
			WITH latest_approved AS (
			  SELECT DISTINCT ON (e."StudentSubmissionId") e."StudentSubmissionId", e."TenantId", e."TotalMarks", e."RubricId"
			  FROM "academic_RubricEvaluations" e WHERE e."Status" = 2 AND NOT e."IsDeleted"
			  ORDER BY e."StudentSubmissionId", COALESCE(e."LastModificationTime", e."CreationTime") DESC, e."Id" DESC
			)
			UPDATE "academic_StudentHomeworkAssignmentSubmissions" s SET "TotalMarks" = r."TotalMarks"
			FROM latest_approved e JOIN "academic_AssessmentRubrics" r ON r."Id" = e."RubricId"
			WHERE s."Id" = e."StudentSubmissionId" AND s."TenantId" IS NOT DISTINCT FROM e."TenantId"
			  AND r."TenantId" IS NOT DISTINCT FROM e."TenantId" AND r."ScoringModel" = 2
			  AND NOT s."IsDeleted" AND s."Marks" = e."TotalMarks"
			  AND s."TotalMarks" = (r."ExtraProperties"::jsonb->>'LegacyRubricTotalMarks')::numeric;
			""");
		migrationBuilder.DropColumn("MaxMarks", "academic_AssessmentRubricCriteria");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<decimal>("MaxMarks", "academic_AssessmentRubricCriteria", "numeric(10,2)", nullable: false, defaultValue: 0m, precision: 10, scale: 2);
		migrationBuilder.Sql("""
			UPDATE "academic_AssessmentRubricCriteria" c
			SET "MaxMarks" = CASE WHEN r."ScoringModel" = 2 THEN COALESCE(
			    (r."ExtraProperties"::jsonb->'LegacyCriterionMaxMarks'->>c."Id"::text)::numeric,
			    (SELECT MAX(COALESCE(l->>'Score', l->>'score')::numeric) FROM jsonb_array_elements(r."PerformanceLevelsJson"::jsonb) l), 0)
			    ELSE COALESCE(c."Weight", c."Points", 0) END
			FROM "academic_AssessmentRubrics" r WHERE r."Id" = c."RubricId";

			WITH latest_approved AS (
			  SELECT DISTINCT ON (e."StudentSubmissionId") e."StudentSubmissionId", e."TenantId", e."TotalMarks", e."RubricId"
			  FROM "academic_RubricEvaluations" e WHERE e."Status" = 2 AND NOT e."IsDeleted"
			  ORDER BY e."StudentSubmissionId", COALESCE(e."LastModificationTime", e."CreationTime") DESC, e."Id" DESC
			)
			UPDATE "academic_StudentHomeworkAssignmentSubmissions" s
			SET "TotalMarks" = COALESCE((r."ExtraProperties"::jsonb->>'LegacyRubricTotalMarks')::numeric, r."TotalMarks")
			FROM latest_approved e JOIN "academic_AssessmentRubrics" r ON r."Id" = e."RubricId"
			WHERE s."Id" = e."StudentSubmissionId" AND s."TenantId" IS NOT DISTINCT FROM e."TenantId"
			  AND r."TenantId" IS NOT DISTINCT FROM e."TenantId" AND r."ScoringModel" = 2
			  AND NOT s."IsDeleted" AND s."Marks" = e."TotalMarks" AND s."TotalMarks" = r."TotalMarks";

			UPDATE "academic_AssessmentRubrics" r
			SET "TotalMarks" = COALESCE((r."ExtraProperties"::jsonb->>'LegacyRubricTotalMarks')::numeric,
			    (SELECT SUM(c."MaxMarks") FROM "academic_AssessmentRubricCriteria" c WHERE c."RubricId" = r."Id" AND NOT c."IsDeleted")),
			    "PerformanceLevelsJson" = CASE WHEN r."ExtraProperties"::jsonb ? 'LegacyPerformanceLevelsJson'
			      THEN r."ExtraProperties"::jsonb->>'LegacyPerformanceLevelsJson' ELSE r."PerformanceLevelsJson" END,
			    "ExtraProperties" = (r."ExtraProperties"::jsonb - 'LegacyCriterionMaxMarks' - 'LegacyRubricTotalMarks' - 'LegacyPerformanceLevelsJson')::text
			WHERE r."ScoringModel" = 2;
			""");
		migrationBuilder.DropColumn("Points", "academic_AssessmentRubricCriteria");
		migrationBuilder.DropColumn("Weight", "academic_AssessmentRubricCriteria");
	}
}
