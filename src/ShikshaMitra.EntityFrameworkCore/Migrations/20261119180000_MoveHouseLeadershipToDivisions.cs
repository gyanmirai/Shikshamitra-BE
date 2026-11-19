using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20261119180000_MoveHouseLeadershipToDivisions")]
public class MoveHouseLeadershipToDivisions : Migration
{
	private const string DivisionHouseLeaderForeignKey = "FK_academic_Divisions_AppStudentEnrollments_HouseLeaderEnrollm~";
	private const string LegacyLeaderForeignKey = "FK_AppHouseSettings_AppStudentEnrollments_LeaderEnrollmentId";

	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<Guid>(
			name: "HouseLeaderEnrollmentId", table: "academic_Divisions", type: "uuid", nullable: true);

		// Keep the preflight and the transfer in the same transaction as the schema change.
		migrationBuilder.Sql("""
			LOCK TABLE "AppHouseSettings", "AppStudentEnrollments", "AppStudents", "academic_Standards",
			    "academic_EducationMediumStages", "academic_EducationMediums", "academic_EducationBoards"
			    IN SHARE ROW EXCLUSIVE MODE;

			CREATE TEMP TABLE house_leader_transfer ON COMMIT DROP AS
			SELECT e."Id" AS enrollment_id, d."Id" AS division_id
			FROM "AppHouseSettings" h
			JOIN "AppStudentEnrollments" e ON e."Id" = h."LeaderEnrollmentId" AND e."HouseId" = h."Id"
			    AND e."TenantId" IS NOT DISTINCT FROM h."TenantId" AND e."IsActive" AND NOT e."IsDeleted"
			JOIN "AppStudents" s ON s."Id" = e."StudentId" AND s."Status" = 6 AND NOT s."IsDeleted"
			    AND s."TenantId" IS NOT DISTINCT FROM h."TenantId"
			JOIN "academic_Divisions" d ON d."Id" = e."DivisionId" AND d."StandardId" = e."StandardId"
			    AND d."TenantId" IS NOT DISTINCT FROM h."TenantId" AND NOT d."IsDeleted"
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
			WHERE NOT h."IsDeleted" AND h."LeaderEnrollmentId" IS NOT NULL;

			DO $$ BEGIN
			    IF EXISTS (SELECT 1 FROM house_leader_transfer GROUP BY division_id HAVING count(*) > 1) THEN
			        RAISE EXCEPTION 'Multiple legacy house leaders share a division. Run scripts/students/house-leaders-preflight.sql and resolve the conflict before retrying.';
			    END IF;
			    IF (SELECT count(*) FROM house_leader_transfer) <> (SELECT count(*) FROM "AppHouseSettings"
			        WHERE NOT "IsDeleted" AND "LeaderEnrollmentId" IS NOT NULL) THEN
			        RAISE EXCEPTION 'A legacy house leader has no eligible current division. Run scripts/students/house-leaders-preflight.sql and review the appointment before retrying.';
			    END IF;
			END $$;

			UPDATE "academic_Divisions" d
			SET "HouseLeaderEnrollmentId" = t.enrollment_id,
			    "LeadershipRevision" = d."LeadershipRevision" + 1,
			    "ConcurrencyStamp" = gen_random_uuid()::text
			FROM house_leader_transfer t WHERE t.division_id = d."Id";
			""");

		migrationBuilder.CreateIndex(
			name: "IX_academic_Divisions_HouseLeaderEnrollmentId", table: "academic_Divisions", column: "HouseLeaderEnrollmentId");
		migrationBuilder.AddForeignKey(
			name: DivisionHouseLeaderForeignKey,
			table: "academic_Divisions", column: "HouseLeaderEnrollmentId",
			principalTable: "AppStudentEnrollments", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

		migrationBuilder.DropForeignKey(name: LegacyLeaderForeignKey, table: "AppHouseSettings");
		migrationBuilder.DropIndex(name: "IX_AppHouseSettings_LeaderEnrollmentId", table: "AppHouseSettings");
		migrationBuilder.DropColumn(name: "LeaderEnrollmentId", table: "AppHouseSettings");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<Guid>(
			name: "LeaderEnrollmentId", table: "AppHouseSettings", type: "uuid", nullable: true);
		migrationBuilder.Sql("""
			LOCK TABLE "academic_Divisions", "AppStudentEnrollments" IN SHARE ROW EXCLUSIVE MODE;

			CREATE TEMP TABLE house_leader_restore ON COMMIT DROP AS
			SELECT h."Id" AS house_id, e."Id" AS enrollment_id
			FROM "academic_Divisions" d
			JOIN "AppStudentEnrollments" e ON e."Id" = d."HouseLeaderEnrollmentId"
			    AND e."DivisionId" = d."Id" AND e."TenantId" IS NOT DISTINCT FROM d."TenantId"
			    AND e."IsActive" AND NOT e."IsDeleted"
			JOIN "AppHouseSettings" h ON h."Id" = e."HouseId"
			    AND h."TenantId" IS NOT DISTINCT FROM d."TenantId" AND NOT h."IsDeleted"
			WHERE NOT d."IsDeleted";

			DO $$ BEGIN
			    IF EXISTS (SELECT 1 FROM house_leader_restore GROUP BY house_id HAVING count(*) > 1)
			        OR (SELECT count(*) FROM house_leader_restore) <> (SELECT count(*) FROM "academic_Divisions"
			            WHERE NOT "IsDeleted" AND "HouseLeaderEnrollmentId" IS NOT NULL) THEN
			        RAISE EXCEPTION 'Current house leader appointments cannot be represented under the former one-leader-per-house rule. Resolve the appointments before rolling back.';
			    END IF;
			END $$;

			UPDATE "AppHouseSettings" h
			SET "LeaderEnrollmentId" = t.enrollment_id, "ConcurrencyStamp" = gen_random_uuid()::text
			FROM house_leader_restore t WHERE h."Id" = t.house_id;
			""");
		migrationBuilder.CreateIndex(
			name: "IX_AppHouseSettings_LeaderEnrollmentId", table: "AppHouseSettings", column: "LeaderEnrollmentId");
		migrationBuilder.AddForeignKey(
			name: LegacyLeaderForeignKey, table: "AppHouseSettings", column: "LeaderEnrollmentId",
			principalTable: "AppStudentEnrollments", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
		migrationBuilder.DropForeignKey(name: DivisionHouseLeaderForeignKey, table: "academic_Divisions");
		migrationBuilder.DropIndex(name: "IX_academic_Divisions_HouseLeaderEnrollmentId", table: "academic_Divisions");
		migrationBuilder.DropColumn(name: "HouseLeaderEnrollmentId", table: "academic_Divisions");
	}
}
