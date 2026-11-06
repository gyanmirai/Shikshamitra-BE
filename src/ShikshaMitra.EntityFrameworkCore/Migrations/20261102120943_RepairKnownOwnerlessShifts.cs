using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20261102120943_RepairKnownOwnerlessShifts")]
public sealed class RepairKnownOwnerlessShifts : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("""
			DO $$
			BEGIN
			  -- Older deployments that already completed the cutover must not rewrite HR baselines.
			  IF EXISTS (SELECT 1 FROM "__EFMigrationsHistory"
			             WHERE "MigrationId" = '20261102120944_SeparateEmployeeWorkShifts') THEN RETURN; END IF;
			  IF to_regclass('"HR_WorkShifts"') IS NOT NULL THEN
			    RAISE EXCEPTION 'HR work shift table exists without the recorded cutover migration.';
			  END IF;

			  -- These are the two verified legacy definitions, not an ownership inference
			  -- for every ownerless shift in the database.
			  IF EXISTS (
			    SELECT 1 FROM "academic_ShiftManagements" s
			    WHERE s."Id" IN ('3a2402e9-5c2b-b6b2-115e-9ba536bda72a',
			                     '3a2402e9-5c2b-f11f-c723-987d258a8d09')
			      AND (s."TenantId" IS NOT NULL OR
			           (s."Id" = '3a2402e9-5c2b-b6b2-115e-9ba536bda72a' AND
			            (s."ShiftName" <> 'Afternoon Shift' OR s."StartTime" <> TIME '12:30' OR s."EndTime" <> TIME '17:30')) OR
			           (s."Id" = '3a2402e9-5c2b-f11f-c723-987d258a8d09' AND
			            (s."ShiftName" <> 'Full Day Shift' OR s."StartTime" <> TIME '07:00' OR s."EndTime" <> TIME '17:30')))
			  ) OR EXISTS (
			    SELECT 1 FROM "AppEmployees" e
			    WHERE e."ShiftId" IN ('3a2402e9-5c2b-b6b2-115e-9ba536bda72a',
			                           '3a2402e9-5c2b-f11f-c723-987d258a8d09')
			      AND e."TenantId" IS DISTINCT FROM '11111111-1111-1111-1111-111111111111'::uuid
			  ) OR EXISTS (
			    SELECT 1 FROM "academic_Standards" st
			    WHERE st."ShiftId" = '3a2402e9-5c2b-b6b2-115e-9ba536bda72a'
			      AND st."TenantId" IS DISTINCT FROM '11111111-1111-1111-1111-111111111111'::uuid
			      AND st."TenantId" IS DISTINCT FROM '3a244aef-f540-277d-9cf5-9bd861f93b83'::uuid
			  ) OR EXISTS (
			    SELECT 1 FROM "academic_Standards" st
			    WHERE st."ShiftId" = '3a2402e9-5c2b-f11f-c723-987d258a8d09'
			  ) THEN
			    RAISE EXCEPTION 'Verified legacy shift repair assumptions changed; inspect references before cutover.';
			  END IF;

			  CREATE TEMP TABLE verified_shift_repair_map (
			    old_id uuid NOT NULL, tenant_id uuid NOT NULL, new_id uuid NOT NULL,
			    PRIMARY KEY (old_id, tenant_id)
			  ) ON COMMIT DROP;

			  INSERT INTO verified_shift_repair_map (old_id, tenant_id, new_id)
			  SELECT refs.old_id, refs.tenant_id,
			         md5('verified-ownerless-shift:' || refs.old_id::text || ':' || refs.tenant_id::text)::uuid
			  FROM (
			    SELECT DISTINCT e."ShiftId" AS old_id, e."TenantId" AS tenant_id
			    FROM "AppEmployees" e
			    WHERE e."ShiftId" IN ('3a2402e9-5c2b-b6b2-115e-9ba536bda72a',
			                           '3a2402e9-5c2b-f11f-c723-987d258a8d09')
			    UNION
			    SELECT DISTINCT st."ShiftId", st."TenantId"
			    FROM "academic_Standards" st
			    WHERE st."ShiftId" = '3a2402e9-5c2b-b6b2-115e-9ba536bda72a'
			  ) refs;

			  INSERT INTO "academic_ShiftManagements"
			    ("Id", "TenantId", "ShiftName", "StartTime", "EndTime", "ExtraProperties", "ConcurrencyStamp")
			  SELECT m.new_id, m.tenant_id, s."ShiftName", s."StartTime", s."EndTime",
			         s."ExtraProperties", md5(m.new_id::text)
			  FROM verified_shift_repair_map m
			  JOIN "academic_ShiftManagements" s ON s."Id" = m.old_id;

			  UPDATE "AppEmployees" e SET "ShiftId" = m.new_id
			  FROM verified_shift_repair_map m
			  WHERE e."ShiftId" = m.old_id AND e."TenantId" = m.tenant_id;

			  UPDATE "academic_Standards" st SET "ShiftId" = m.new_id
			  FROM verified_shift_repair_map m
			  WHERE st."ShiftId" = m.old_id AND st."TenantId" = m.tenant_id;

			  IF EXISTS (
			    SELECT 1 FROM "AppEmployees" e
			    WHERE e."ShiftId" IN ('3a2402e9-5c2b-b6b2-115e-9ba536bda72a',
			                           '3a2402e9-5c2b-f11f-c723-987d258a8d09')
			  ) OR EXISTS (
			    SELECT 1 FROM "academic_Standards" st
			    WHERE st."ShiftId" IN ('3a2402e9-5c2b-b6b2-115e-9ba536bda72a',
			                           '3a2402e9-5c2b-f11f-c723-987d258a8d09')
			  ) THEN
			    RAISE EXCEPTION 'Verified legacy shift references remain after repair.';
			  END IF;
			END $$;
			""");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		throw new InvalidOperationException("Restore the verified pre-cutover backup; this repair changes referenced shift identities.");
	}
}
