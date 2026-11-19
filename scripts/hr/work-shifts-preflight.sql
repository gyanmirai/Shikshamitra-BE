-- Read-only preflight. Run before the coordinated maintenance window.
-- Verify the target database first. Detailed rows contain sensitive identifiers;
-- share only the count summary in deployment logs. Counts may overlap.
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SELECT 'MISSING_SHIFT' AS "Status", count(*) AS "Count"
FROM "AppEmployees" e LEFT JOIN "academic_ShiftManagements" s ON s."Id"=e."ShiftId"
WHERE e."ShiftId" IS NOT NULL AND s."Id" IS NULL
UNION ALL
SELECT 'OWNERLESS_EMPLOYEE', count(*) FROM "AppEmployees" WHERE "ShiftId" IS NOT NULL AND "TenantId" IS NULL
UNION ALL
SELECT 'OWNERLESS_SHIFT', count(*) FROM "AppEmployees" e JOIN "academic_ShiftManagements" s ON s."Id"=e."ShiftId" WHERE s."TenantId" IS NULL
UNION ALL
SELECT 'FOREIGN_TENANT', count(*) FROM "AppEmployees" e JOIN "academic_ShiftManagements" s ON s."Id"=e."ShiftId" WHERE e."TenantId" <> s."TenantId"
UNION ALL
SELECT 'INVALID_TIMES', count(*) FROM "academic_ShiftManagements" WHERE "TenantId" IS NOT NULL AND "EndTime" <= "StartTime"
UNION ALL
SELECT 'EMPTY_NAME', count(*) FROM "academic_ShiftManagements" WHERE "TenantId" IS NOT NULL AND length(trim("ShiftName"))=0
UNION ALL
SELECT 'NAME_TOO_LONG', count(*) FROM "academic_ShiftManagements" WHERE "TenantId" IS NOT NULL AND length("ShiftName")>128;

SELECT e."Id" AS "EmployeeId", e."TenantId", e."ShiftId", CASE
 WHEN e."ShiftId" IS NULL THEN 'UNASSIGNED'
 WHEN s."Id" IS NULL THEN 'MISSING_SHIFT'
 WHEN e."TenantId" IS NULL OR s."TenantId" IS NULL THEN 'UNRESOLVED_OWNER'
 WHEN e."TenantId" <> s."TenantId" THEN 'FOREIGN_TENANT'
 WHEN s."EndTime" <= s."StartTime" THEN 'INVALID_TIMES'
 WHEN length(trim(s."ShiftName"))=0 THEN 'EMPTY_NAME'
 WHEN length(s."ShiftName")>128 THEN 'NAME_TOO_LONG'
 ELSE 'VALID' END AS "Status"
FROM "AppEmployees" e LEFT JOIN "academic_ShiftManagements" s ON s."Id"=e."ShiftId"
ORDER BY "Status", e."Id";
SELECT "Id", "TenantId", "ShiftName", "StartTime", "EndTime"
FROM "academic_ShiftManagements"
WHERE "TenantId" IS NOT NULL AND ("EndTime" <= "StartTime" OR length(trim("ShiftName"))=0 OR length("ShiftName")>128);
ROLLBACK;
