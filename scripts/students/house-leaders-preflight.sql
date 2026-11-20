-- Historical preflight for the superseded transfer of House leaders to divisions.
-- The house-scoped recovery migration does not require this report.
-- Run against the intended database before MoveHouseLeadershipToDivisions.
-- Every non-deleted legacy appointment must resolve to a single eligible division.
WITH candidates AS (
    SELECT h."TenantId", h."Id" AS house_id, h."Name" AS house_name,
           h."LeaderEnrollmentId" AS enrollment_id,
           concat_ws(' ', s."FirstName", s."LastName") AS student_name,
           c."Name" AS class_name, d."Name" AS division_name, d."Id" AS division_id,
           e."Id" IS NOT NULL AND s."Id" IS NOT NULL AND b."Id" IS NOT NULL AS eligible
    FROM "AppHouseSettings" h
    LEFT JOIN "AppStudentEnrollments" e ON e."Id" = h."LeaderEnrollmentId"
        AND e."TenantId" IS NOT DISTINCT FROM h."TenantId"
        AND e."HouseId" = h."Id" AND e."IsActive" AND NOT e."IsDeleted"
    LEFT JOIN "AppStudents" s ON s."Id" = e."StudentId"
        AND s."TenantId" IS NOT DISTINCT FROM h."TenantId" AND s."Status" = 6 AND NOT s."IsDeleted"
    LEFT JOIN "academic_Divisions" d ON d."Id" = e."DivisionId" AND d."StandardId" = e."StandardId"
        AND d."TenantId" IS NOT DISTINCT FROM h."TenantId" AND NOT d."IsDeleted"
    LEFT JOIN "academic_Standards" c ON c."Id" = d."StandardId"
        AND c."TenantId" IS NOT DISTINCT FROM h."TenantId" AND NOT c."IsDeleted"
    LEFT JOIN "academic_EducationMediumStages" st ON st."Id" = c."EducationMediumStageId"
        AND st."EducationMediumId" = h."AcademicMediumId"
        AND st."TenantId" IS NOT DISTINCT FROM h."TenantId" AND NOT st."IsDeleted"
    LEFT JOIN "academic_EducationMediums" m ON m."Id" = st."EducationMediumId"
        AND m."TenantId" IS NOT DISTINCT FROM h."TenantId" AND NOT m."IsDeleted"
    LEFT JOIN "academic_EducationBoards" b ON b."Id" = m."EducationBoardId"
        AND b."AcademicYearId" = e."AcademicYearId"
        AND b."TenantId" IS NOT DISTINCT FROM h."TenantId" AND NOT b."IsDeleted"
    WHERE NOT h."IsDeleted" AND h."LeaderEnrollmentId" IS NOT NULL
), division_counts AS (
    SELECT candidates.*, count(*) FILTER (WHERE eligible) OVER (PARTITION BY division_id) AS nominees
    FROM candidates
)
SELECT jsonb_build_object(
    'database', current_database(),
    'migrationHead', (SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1),
    'eligibleCount', count(*) FILTER (WHERE eligible),
    'conflictDivisionCount', count(DISTINCT division_id) FILTER (WHERE nominees > 1 AND eligible),
    'invalidCount', count(*) FILTER (WHERE NOT eligible),
    'appointments', coalesce(jsonb_agg(to_jsonb(division_counts) ORDER BY class_name, division_name, house_name), '[]')
) AS result
FROM division_counts;
