-- Read-only preflight. Run before the coordinated maintenance window.
SELECT e."Id" AS "EmployeeId", e."TenantId", e."ShiftId", CASE
 WHEN e."ShiftId" IS NULL THEN 'UNASSIGNED'
 WHEN s."Id" IS NULL THEN 'MISSING_SHIFT'
 WHEN e."TenantId" IS NULL OR s."TenantId" IS NULL THEN 'UNRESOLVED_OWNER'
 WHEN e."TenantId" <> s."TenantId" THEN 'FOREIGN_TENANT'
 WHEN s."EndTime" <= s."StartTime" THEN 'INVALID_TIMES'
 ELSE 'VALID' END AS "Status"
FROM "AppEmployees" e LEFT JOIN "academic_ShiftManagements" s ON s."Id"=e."ShiftId"
ORDER BY "Status", e."Id";
SELECT "Id", "TenantId", "ShiftName", "StartTime", "EndTime"
FROM "academic_ShiftManagements"
WHERE "TenantId" IS NOT NULL AND ("EndTime" <= "StartTime" OR length(trim("ShiftName"))=0 OR length("ShiftName")>128);
