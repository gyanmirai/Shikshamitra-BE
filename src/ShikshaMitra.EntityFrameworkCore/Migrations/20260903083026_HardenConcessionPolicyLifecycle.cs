using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class HardenConcessionPolicyLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConcessionApplicableFeeHeadIdsJson",
                table: "Finance_StudentFeeStructures",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "ConcessionFeeHeadScope",
                table: "Finance_StudentFeeStructures",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ConcessionApplicableFeeHeadIdsJson",
                table: "Finance_FeeStructures",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "ConcessionFeeHeadScope",
                table: "Finance_FeeStructures",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "RefundedAmount",
                table: "Finance_DueFees",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<decimal>(
                name: "PaidAmount",
                table: "Finance_DueFees",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<decimal>(
                name: "OutstandingAmount",
                table: "Finance_DueFees",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<decimal>(
                name: "OriginalAmount",
                table: "Finance_DueFees",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<decimal>(
                name: "NetAmount",
                table: "Finance_DueFees",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<decimal>(
                name: "AdjustmentCreditAmount",
                table: "Finance_DueFees",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AddColumn<decimal>(
                name: "ConcessionAmount",
                table: "Finance_DueFees",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicYearId",
                table: "Finance_ConcessionRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "BoardId",
                table: "Finance_ConcessionRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "DivisionId",
                table: "Finance_ConcessionRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EducationMediumId",
                table: "Finance_ConcessionRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "StandardId",
                table: "Finance_ConcessionRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "MaximumFixedAmount",
                table: "Finance_ConcessionPolicies",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            // Preserve the historical academic/class scope that was stored in ABP ExtraProperties.
            // Requests created after this migration write both the concrete columns and the audit JSON.
            migrationBuilder.Sql("""
                UPDATE "Finance_ConcessionRequests"
                SET "AcademicYearId" = COALESCE(NULLIF(("ExtraProperties"::jsonb ->> 'AcademicYearId'), '')::uuid, "AcademicYearId"),
                    "BoardId" = COALESCE(NULLIF(("ExtraProperties"::jsonb ->> 'BoardId'), '')::uuid, "BoardId"),
                    "EducationMediumId" = COALESCE(NULLIF(("ExtraProperties"::jsonb ->> 'EducationMediumId'), '')::uuid, "EducationMediumId"),
                    "StandardId" = COALESCE(NULLIF(("ExtraProperties"::jsonb ->> 'StandardId'), '')::uuid, "StandardId"),
                    "DivisionId" = COALESCE(NULLIF(("ExtraProperties"::jsonb ->> 'DivisionId'), '')::uuid, "DivisionId")
                """);

            // Existing assignments predate policy snapshots, but their academic year can be
            // recovered deterministically from the assigned fee structure metadata.
            migrationBuilder.Sql("""
                UPDATE "Finance_StudentFeeStructures" AS sfs
                SET "AcademicYearId" = fsm."AcademicYearId"
                FROM "Finance_FeeStructures" AS fs
                INNER JOIN "Finance_FeeStructureMetadatas" AS fsm
                    ON fsm."Id" = fs."FeeStructureMetadataId"
                WHERE sfs."FeeStructureStandardId" = fs."Id"
                  AND fsm."AcademicYearId" IS NOT NULL
                """);

            // NetAmount is the aggregate credit balance and already included historical
            // concessions. Split that value into the new dedicated balance without changing it.
            migrationBuilder.Sql("""
                UPDATE "Finance_DueFees"
                SET "ConcessionAmount" = GREATEST(
                    0,
                    "NetAmount" - "AdjustmentCreditAmount")
                """);

            // Fee-head applicability now belongs to each fee-structure attachment. Existing
            // attachments intentionally default to all currently eligible fee heads; the removed
            // policy-level selection is not copied because it may not belong to the structure.
            migrationBuilder.DropColumn(
                name: "ApplicableFeeHeadIdsJson",
                table: "Finance_ConcessionPolicies");

            migrationBuilder.DropColumn(
                name: "FeeHeadScope",
                table: "Finance_ConcessionPolicies");

            migrationBuilder.CreateTable(
                name: "Finance_ConcessionReversals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcessionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessionDueFeeAllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueFeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeHeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReversedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReversedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_ConcessionReversals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_ConcessionReversals_Finance_ConcessionRequests_Conc~",
                        column: x => x.ConcessionRequestId,
                        principalTable: "Finance_ConcessionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ConcessionRequests_AcademicYearId_StudentFeeStructu~",
                table: "Finance_ConcessionRequests",
                columns: new[] { "AcademicYearId", "StudentFeeStructureId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ConcessionReversals_ConcessionDueFeeAllocationId",
                table: "Finance_ConcessionReversals",
                column: "ConcessionDueFeeAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ConcessionReversals_ConcessionRequestId",
                table: "Finance_ConcessionReversals",
                column: "ConcessionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ConcessionReversals_DueFeeId",
                table: "Finance_ConcessionReversals",
                column: "DueFeeId");

            // Enforce the journal's append-only contract at the database boundary as well as in
            // the domain model. Corrections are represented by an additional reversal entry.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION "Finance_PreventConcessionReversalMutation"()
                RETURNS trigger AS $BODY$
                BEGIN
                    RAISE EXCEPTION 'Finance_ConcessionReversals is an immutable journal';
                END;
                $BODY$ LANGUAGE plpgsql;

                CREATE TRIGGER "TR_Finance_ConcessionReversals_Immutable"
                BEFORE UPDATE OR DELETE ON "Finance_ConcessionReversals"
                FOR EACH ROW EXECUTE FUNCTION "Finance_PreventConcessionReversalMutation"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "TR_Finance_ConcessionReversals_Immutable"
                    ON "Finance_ConcessionReversals";
                DROP FUNCTION IF EXISTS "Finance_PreventConcessionReversalMutation"();
                """);

            migrationBuilder.DropTable(
                name: "Finance_ConcessionReversals");

            migrationBuilder.DropIndex(
                name: "IX_Finance_ConcessionRequests_AcademicYearId_StudentFeeStructu~",
                table: "Finance_ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "ConcessionApplicableFeeHeadIdsJson",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "ConcessionFeeHeadScope",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "ConcessionApplicableFeeHeadIdsJson",
                table: "Finance_FeeStructures");

            migrationBuilder.DropColumn(
                name: "ConcessionFeeHeadScope",
                table: "Finance_FeeStructures");

            migrationBuilder.DropColumn(
                name: "ConcessionAmount",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "Finance_ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "BoardId",
                table: "Finance_ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "DivisionId",
                table: "Finance_ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "EducationMediumId",
                table: "Finance_ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "StandardId",
                table: "Finance_ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "MaximumFixedAmount",
                table: "Finance_ConcessionPolicies");

            migrationBuilder.AlterColumn<double>(
                name: "RefundedAmount",
                table: "Finance_DueFees",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<double>(
                name: "PaidAmount",
                table: "Finance_DueFees",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<double>(
                name: "OutstandingAmount",
                table: "Finance_DueFees",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<double>(
                name: "OriginalAmount",
                table: "Finance_DueFees",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<double>(
                name: "NetAmount",
                table: "Finance_DueFees",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<double>(
                name: "AdjustmentCreditAmount",
                table: "Finance_DueFees",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<string>(
                name: "ApplicableFeeHeadIdsJson",
                table: "Finance_ConcessionPolicies",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "FeeHeadScope",
                table: "Finance_ConcessionPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
