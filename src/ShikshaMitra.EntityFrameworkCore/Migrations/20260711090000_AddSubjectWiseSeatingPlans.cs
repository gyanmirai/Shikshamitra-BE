using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ShikshaMitraDbContext))]
    [Migration("20260711090000_AddSubjectWiseSeatingPlans")]
    public partial class AddSubjectWiseSeatingPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "AppExamSeatingPlans",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "All Subjects");

            migrationBuilder.AddColumn<int>(
                name: "ScopeType",
                table: "AppExamSeatingPlans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SeatingPlanId",
                table: "AppExamSeatAllocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppExamSeatingPlanSubjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeatingPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamSubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppExamSeatingPlanSubjects", x => x.Id);
                });

            migrationBuilder.Sql("""
                INSERT INTO "AppExamSeatingPlans" (
                    "Id", "TenantId", "ExamId", "Name", "ScopeType", "Status", "RoomIdsCsv",
                    "AllocationRulesJson", "ExcludedStudentIdsCsv", "SpecialRequirementStudentIdsCsv",
                    "IsLocked", "CreationTime", "IsDeleted"
                )
                SELECT (md5(random()::text || clock_timestamp()::text))::uuid, e."TenantId", e."Id", 'All Subjects', 0, 0, '', '', '', '', false, NOW(), false
                FROM "AppExams" e
                WHERE EXISTS (
                    SELECT 1 FROM "AppExamSeatAllocations" a WHERE a."ExamId" = e."Id"
                )
                AND NOT EXISTS (
                    SELECT 1 FROM "AppExamSeatingPlans" p WHERE p."ExamId" = e."Id"
                );
                """);

            migrationBuilder.Sql("""
                UPDATE "AppExamSeatAllocations" a
                SET "SeatingPlanId" = p."Id"
                FROM "AppExamSeatingPlans" p
                WHERE p."ExamId" = a."ExamId"
                  AND p."ScopeType" = 0
                  AND a."SeatingPlanId" IS NULL;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "AppExamSeatingPlanSubjects" ("Id", "TenantId", "SeatingPlanId", "ExamId", "ExamSubjectId", "SubjectId")
                SELECT (md5(random()::text || clock_timestamp()::text))::uuid, p."TenantId", p."Id", es."ExamId", es."Id", es."SubjectId"
                FROM "AppExamSeatingPlans" p
                INNER JOIN "AppExamSubjects" es ON es."ExamId" = p."ExamId"
                WHERE p."ScopeType" = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM "AppExamSeatingPlanSubjects" ps
                      WHERE ps."SeatingPlanId" = p."Id" AND ps."ExamSubjectId" = es."Id"
                  );
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "SeatingPlanId",
                table: "AppExamSeatAllocations",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_AppExamSeatingPlans_ExamId",
                table: "AppExamSeatingPlans");

            migrationBuilder.DropIndex(
                name: "IX_AppExamSeatAllocations_ExamId_StudentId",
                table: "AppExamSeatAllocations");

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSeatingPlans_ExamId",
                table: "AppExamSeatingPlans",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSeatingPlanSubjects_ExamId_ExamSubjectId",
                table: "AppExamSeatingPlanSubjects",
                columns: new[] { "ExamId", "ExamSubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSeatingPlanSubjects_SeatingPlanId_ExamSubjectId",
                table: "AppExamSeatingPlanSubjects",
                columns: new[] { "SeatingPlanId", "ExamSubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSeatAllocations_ExamId",
                table: "AppExamSeatAllocations",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSeatAllocations_SeatingPlanId_RoomId_SeatNumber",
                table: "AppExamSeatAllocations",
                columns: new[] { "SeatingPlanId", "RoomId", "SeatNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSeatAllocations_SeatingPlanId_StudentId",
                table: "AppExamSeatAllocations",
                columns: new[] { "SeatingPlanId", "StudentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppExamSeatAllocations_SeatingPlanId_StudentId",
                table: "AppExamSeatAllocations");

            migrationBuilder.DropIndex(
                name: "IX_AppExamSeatAllocations_SeatingPlanId_RoomId_SeatNumber",
                table: "AppExamSeatAllocations");

            migrationBuilder.DropIndex(
                name: "IX_AppExamSeatAllocations_ExamId",
                table: "AppExamSeatAllocations");

            migrationBuilder.DropTable(
                name: "AppExamSeatingPlanSubjects");

            migrationBuilder.DropIndex(
                name: "IX_AppExamSeatingPlans_ExamId",
                table: "AppExamSeatingPlans");

            migrationBuilder.DropColumn(
                name: "SeatingPlanId",
                table: "AppExamSeatAllocations");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "AppExamSeatingPlans");

            migrationBuilder.DropColumn(
                name: "ScopeType",
                table: "AppExamSeatingPlans");

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSeatingPlans_ExamId",
                table: "AppExamSeatingPlans",
                column: "ExamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppExamSeatAllocations_ExamId_StudentId",
                table: "AppExamSeatAllocations",
                columns: new[] { "ExamId", "StudentId" },
                unique: true);
        }
    }
}
