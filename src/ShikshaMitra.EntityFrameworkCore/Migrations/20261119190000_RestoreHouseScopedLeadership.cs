using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ShikshaMitra.Migrations;

public partial class RestoreHouseScopedLeadership : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
		LOCK TABLE "AppHouseSettings", "academic_Divisions", "AppStudentEnrollments", "AppStudents",
		    "academic_Standards", "academic_EducationMediumStages", "academic_EducationMediums",
		    "academic_EducationBoards" IN SHARE ROW EXCLUSIVE MODE;

		DO $restore$
		DECLARE
		    has_house_slot boolean := EXISTS (SELECT 1 FROM pg_attribute
		        WHERE attrelid = '"AppHouseSettings"'::regclass AND attname = 'LeaderEnrollmentId' AND NOT attisdropped);
		    has_division_slot boolean := EXISTS (SELECT 1 FROM pg_attribute
		        WHERE attrelid = '"academic_Divisions"'::regclass AND attname = 'HouseLeaderEnrollmentId' AND NOT attisdropped);
		BEGIN
		    IF has_house_slot AND NOT has_division_slot THEN
		        RETURN;
		    END IF;
		    IF has_house_slot OR NOT has_division_slot THEN
		        RAISE EXCEPTION 'Unrecognized House leadership schema. Review the database before retrying.';
		    END IF;

		    CREATE TEMP TABLE restore_house_appointments ON COMMIT DROP AS
		    SELECT h."Id" AS house_id, e."Id" AS enrollment_id
		    FROM "academic_Divisions" d
		    JOIN "AppStudentEnrollments" e ON e."Id" = d."HouseLeaderEnrollmentId"
		        AND e."DivisionId" = d."Id" AND e."StandardId" = d."StandardId"
		        AND e."TenantId" IS NOT DISTINCT FROM d."TenantId" AND e."IsActive" AND NOT e."IsDeleted"
		    JOIN "AppHouseSettings" h ON h."Id" = e."HouseId"
		        AND h."TenantId" IS NOT DISTINCT FROM d."TenantId" AND NOT h."IsDeleted"
		    JOIN "AppStudents" s ON s."Id" = e."StudentId"
		        AND s."TenantId" IS NOT DISTINCT FROM h."TenantId" AND s."Status" = 6 AND NOT s."IsDeleted"
		    JOIN "academic_Standards" c ON c."Id" = d."StandardId"
		        AND c."TenantId" IS NOT DISTINCT FROM h."TenantId" AND NOT c."IsDeleted"
		    JOIN "academic_EducationMediumStages" st ON st."Id" = c."EducationMediumStageId"
		        AND st."EducationMediumId" = h."AcademicMediumId"
		        AND st."TenantId" IS NOT DISTINCT FROM h."TenantId" AND NOT st."IsDeleted"
		    JOIN "academic_EducationMediums" m ON m."Id" = st."EducationMediumId"
		        AND m."TenantId" IS NOT DISTINCT FROM h."TenantId" AND NOT m."IsDeleted"
		    JOIN "academic_EducationBoards" b ON b."Id" = m."EducationBoardId"
		        AND b."AcademicYearId" = e."AcademicYearId"
		        AND b."TenantId" IS NOT DISTINCT FROM h."TenantId" AND NOT b."IsDeleted"
		    WHERE NOT d."IsDeleted";

		    IF EXISTS (SELECT 1 FROM restore_house_appointments GROUP BY house_id HAVING count(*) > 1) THEN
		        RAISE EXCEPTION 'Multiple division House leaders belong to the same house. Review those appointments before restoring one leader per house.';
		    END IF;
		    IF (SELECT count(*) FROM restore_house_appointments) <>
		        (SELECT count(*) FROM "academic_Divisions" WHERE "HouseLeaderEnrollmentId" IS NOT NULL) THEN
		        RAISE EXCEPTION 'A division House leader cannot be transferred to an eligible house. Review the appointment before retrying.';
		    END IF;

		    ALTER TABLE "AppHouseSettings" ADD COLUMN "LeaderEnrollmentId" uuid;
		    UPDATE "AppHouseSettings" h
		    SET "LeaderEnrollmentId" = a.enrollment_id,
		        "MembershipRevision" = h."MembershipRevision" + 1,
		        "ConcurrencyStamp" = gen_random_uuid()::text
		    FROM restore_house_appointments a WHERE a.house_id = h."Id";
		    CREATE INDEX "IX_AppHouseSettings_LeaderEnrollmentId" ON "AppHouseSettings" ("LeaderEnrollmentId");
		    ALTER TABLE "AppHouseSettings" ADD CONSTRAINT "FK_AppHouseSettings_AppStudentEnrollments_LeaderEnrollmentId"
		        FOREIGN KEY ("LeaderEnrollmentId") REFERENCES "AppStudentEnrollments" ("Id") ON DELETE RESTRICT;
		    ALTER TABLE "academic_Divisions" DROP COLUMN "HouseLeaderEnrollmentId";
		END;
		$restore$;
		""");

	protected override void Down(MigrationBuilder migrationBuilder) =>
		throw new NotSupportedException("Return to division-based House leadership requires reviewed appointment mapping and a coordinated database restore.");
}
