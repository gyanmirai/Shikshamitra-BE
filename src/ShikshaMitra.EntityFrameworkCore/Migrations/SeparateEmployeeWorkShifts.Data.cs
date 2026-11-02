using Microsoft.EntityFrameworkCore.Migrations;
namespace ShikshaMitra.Migrations;

public partial class SeparateEmployeeWorkShifts
{
 private static void ValidateLegacyReferences(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
 DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM "AppEmployees" e LEFT JOIN "academic_ShiftManagements" s ON s."Id"=e."ShiftId"
  WHERE e."ShiftId" IS NOT NULL AND (s."Id" IS NULL OR e."TenantId" IS NULL OR s."TenantId" IS DISTINCT FROM e."TenantId"))
 OR EXISTS (SELECT 1 FROM "academic_ShiftManagements" WHERE "TenantId" IS NOT NULL AND ("EndTime" <= "StartTime" OR length(trim("ShiftName"))=0 OR length("ShiftName")>128))
 THEN RAISE EXCEPTION 'Employee work shift preflight failed. Resolve invalid, missing, ownerless, or foreign-tenant references before cutover.'; END IF;
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
