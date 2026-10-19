\set ON_ERROR_STOP on
-- Run after migration while reporting entry points and old workers are stopped.
-- Save this exact list before cleanup. Never delete unreferenced or non-report objects.
COPY (
    SELECT "TenantId", "Id" AS "ExportJobId", "BlobName"
    FROM "HR_ReportExportJobs"
    WHERE "BlobName" IS NOT NULL
    ORDER BY "TenantId", "Id"
) TO STDOUT WITH (FORMAT CSV, HEADER TRUE);
