\set ON_ERROR_STOP on
\if :{?expected_database}
\else
  \echo 'Set expected_database explicitly. Restore and validate the database first.'
  \quit 3
\endif
\if :{?reporting_cutover_confirmed}
\else
  \echo 'Set reporting_cutover_confirmed=true after stopping old workers and exporting the file manifest.'
  \quit 3
\endif
SELECT current_database() = :'expected_database' AND :'reporting_cutover_confirmed' = 'true' AS correct_target \gset
\if :correct_target
\else
  \echo 'Cutover target or confirmation does not match.'
  \quit 3
\endif
BEGIN;
LOCK TABLE "HR_ReportTemplates", "HR_ReportExportJobs" IN ACCESS EXCLUSIVE MODE;
-- Abort before deleting records if a file reference falls outside the owned namespace.
DO $$ BEGIN
  IF EXISTS (SELECT 1 FROM "HR_ReportExportJobs" WHERE "BlobName" IS NOT NULL AND "BlobName" NOT LIKE 'hr-report-exports/%') THEN
    RAISE EXCEPTION 'Unexpected export file reference. Review the manifest before cleanup.';
  END IF;
END $$;
DELETE FROM "AbpBackgroundJobs" WHERE "JobName" IN (
    'ShikshaMitra.HR.Reports.HrReportExportJobArgs',
    'ShikshaMitra.HR.Reports.HrReportScheduleJobArgs'
);
DELETE FROM "HR_ReportExportJobs";
DELETE FROM "HR_ReportTemplates" WHERE "DefinitionVersion" <> 2;
COMMIT;
