using Microsoft.EntityFrameworkCore.Migrations;
namespace ShikshaMitra.Migrations;

public partial class SeparateEmployeeWorkShifts
{
 private static void ValidateLegacyReferences(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
 DO $$
 DECLARE
  missing_shift bigint;
  ownerless_employee bigint;
  ownerless_shift bigint;
  foreign_tenant bigint;
  invalid_times bigint;
  empty_name bigint;
  name_too_long bigint;
 BEGIN
 SELECT count(*) FILTER (WHERE s."Id" IS NULL),
  count(*) FILTER (WHERE e."TenantId" IS NULL),
  count(*) FILTER (WHERE s."Id" IS NOT NULL AND s."TenantId" IS NULL),
  count(*) FILTER (WHERE e."TenantId" IS NOT NULL AND s."TenantId" IS NOT NULL AND s."TenantId" <> e."TenantId")
 INTO missing_shift, ownerless_employee, ownerless_shift, foreign_tenant
 FROM "AppEmployees" e LEFT JOIN "academic_ShiftManagements" s ON s."Id"=e."ShiftId"
 WHERE e."ShiftId" IS NOT NULL;
 SELECT count(*) FILTER (WHERE "EndTime" <= "StartTime"),
  count(*) FILTER (WHERE length(trim("ShiftName"))=0),
  count(*) FILTER (WHERE length("ShiftName")>128)
 INTO invalid_times, empty_name, name_too_long
 FROM "academic_ShiftManagements" WHERE "TenantId" IS NOT NULL;
 IF missing_shift + ownerless_employee + ownerless_shift + foreign_tenant + invalid_times + empty_name + name_too_long > 0 THEN
  -- Counts only: deployment logs must not expose employee, tenant or shift data.
  RAISE EXCEPTION 'Employee work shift preflight failed. MISSING_SHIFT=%, OWNERLESS_EMPLOYEE=%, OWNERLESS_SHIFT=%, FOREIGN_TENANT=%, INVALID_TIMES=%, EMPTY_NAME=%, NAME_TOO_LONG=%. Counts may overlap. Run scripts/hr/work-shifts-preflight.sql on the verified target and obtain an approved audited repair before cutover.',
   missing_shift, ownerless_employee, ownerless_shift, foreign_tenant, invalid_times, empty_name, name_too_long;
 END IF;
 END $$;
 """);
 private static void PopulateIndependentWorkShifts(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
 INSERT INTO "HR_WorkShifts" ("Id","TenantId","ShiftName","StartTime","EndTime","IsActive","LegacySchoolShiftId","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
 SELECT "Id","TenantId","ShiftName","StartTime","EndTime",true,"Id",'{}',md5("Id"::text || 'hr-shift'),CURRENT_TIMESTAMP,false FROM "academic_ShiftManagements" WHERE "TenantId" IS NOT NULL;
 INSERT INTO "HR_EmployeeShiftAssignments" ("Id","TenantId","EmployeeId","WorkShiftId","EffectiveFrom","EffectiveTo","ShiftName","StartTime","EndTime","Provenance","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
 SELECT md5(e."Id"::text || 'imported-work-shift')::uuid,e."TenantId",e."Id",s."Id",COALESCE(LEAST(e."DateOfJoining",(SELECT MIN(a."AttendanceDate") FROM "AppEmployeeAttendances" a WHERE a."EmployeeId"=e."Id")),DATE '0001-01-01'),NULL,s."ShiftName",s."StartTime",s."EndTime",'ImportedLastKnown','{}',md5(e."Id"::text || 'baseline'),CURRENT_TIMESTAMP,false
 FROM "AppEmployees" e JOIN "HR_WorkShifts" s ON s."Id"=e."ShiftId" AND s."TenantId"=e."TenantId";
 CREATE EXTENSION IF NOT EXISTS btree_gist;
 ALTER TABLE "HR_EmployeeShiftAssignments" ADD CONSTRAINT "EX_HR_EmployeeShiftAssignments_NoOverlap"
 EXCLUDE USING gist ("TenantId" WITH =, "EmployeeId" WITH =, daterange("EffectiveFrom","EffectiveTo",'[)') WITH &&) WHERE (NOT "IsDeleted");
 """);
}
