using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class PersistLibraryStockVerificationResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_LibraryStockCommand_Tenant_Session_Transition",
                table: "AppLibraryStockVerificationCommands");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LibraryStockVerificationCommand_Target",
                table: "AppLibraryStockVerificationCommands");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_LibraryStockDecision_Tenant_Session_Id",
                table: "AppLibraryStockVerificationDecisions",
                columns: new[] { "TenantId", "SessionId", "Id" });

            migrationBuilder.CreateTable(
                name: "AppLibraryStockVerificationResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    RowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    AccessionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Finding = table.Column<int>(type: "integer", nullable: false),
                    BaselineStatus = table.Column<int>(type: "integer", nullable: true),
                    CurrentStatus = table.Column<int>(type: "integer", nullable: true),
                    ExpectedLocation = table.Column<string>(type: "text", nullable: true),
                    ObservedLocation = table.Column<string>(type: "text", nullable: true),
                    ObservationSequence = table.Column<int>(type: "integer", nullable: true),
                    EvidenceRevision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DecisionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryStockVerificationResults", x => x.Id);
                    table.CheckConstraint("CK_LibraryStockResult_Decision", "(\"Finding\" IN (2, 3, 4, 10, 12, 13, 14, 15)) = (\"DecisionId\" IS NOT NULL)");
                    table.CheckConstraint("CK_LibraryStockResult_Finding", "\"Finding\" BETWEEN 1 AND 15");
                    table.CheckConstraint("CK_LibraryStockResult_Position", "\"Position\" > 0");
                    table.CheckConstraint("CK_LibraryStockResult_Source", "\"Source\" IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_LibraryStockResult_Command",
                        columns: x => new { x.TenantId, x.SessionId, x.CommandId },
                        principalTable: "AppLibraryStockVerificationCommands",
                        principalColumns: new[] { "TenantId", "SessionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LibraryStockResult_Decision",
                        columns: x => new { x.TenantId, x.SessionId, x.DecisionId },
                        principalTable: "AppLibraryStockVerificationDecisions",
                        principalColumns: new[] { "TenantId", "SessionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LibraryStockObservation_Tenant_Session_Copy_Sequence",
                table: "AppLibraryStockVerificationObservations",
                columns: new[] { "TenantId", "SessionId", "CopyId", "Sequence" },
                filter: "\"CopyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_LibraryStockCommand_Tenant_Session_Transition",
                table: "AppLibraryStockVerificationCommands",
                columns: new[] { "TenantId", "SessionId", "Kind" },
                unique: true,
                filter: "\"Kind\" IN (3, 4, 6)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LibraryStockVerificationCommand_Target",
                table: "AppLibraryStockVerificationCommands",
                sql: "(\"Kind\" IN (1, 3, 4, 5, 6) AND \"ObservationId\" IS NULL) OR (\"Kind\" = 2 AND \"ObservationId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryStockVerificationResults_TenantId_SessionId_Comma~",
                table: "AppLibraryStockVerificationResults",
                columns: new[] { "TenantId", "SessionId", "CommandId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryStockVerificationResults_TenantId_SessionId_Decis~",
                table: "AppLibraryStockVerificationResults",
                columns: new[] { "TenantId", "SessionId", "DecisionId" });

            migrationBuilder.CreateIndex(
                name: "UX_LibraryStockResult_Tenant_Session_Position",
                table: "AppLibraryStockVerificationResults",
                columns: new[] { "TenantId", "SessionId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_LibraryStockResult_Tenant_Session_Source_Row",
                table: "AppLibraryStockVerificationResults",
                columns: new[] { "TenantId", "SessionId", "Source", "RowId" },
                unique: true);
            migrationBuilder.Sql("""
                CREATE FUNCTION public."AppProtectLibraryStockVerificationResult"()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Published stock verification findings are append-only.';
                    END IF;
                    IF NEW."EvidenceRevision" !~ '^[0-9a-fA-F]{64}$' OR NOT EXISTS (
                        SELECT 1 FROM public."AppLibraryStockVerificationCommands"
                        WHERE "TenantId" = NEW."TenantId" AND "SessionId" = NEW."SessionId"
                            AND "Id" = NEW."CommandId" AND "Kind" = 6
                    ) OR NEW."DecisionId" IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM public."AppLibraryStockVerificationDecisions" AS decision
                        WHERE decision."TenantId" = NEW."TenantId" AND decision."SessionId" = NEW."SessionId"
                            AND decision."Id" = NEW."DecisionId" AND decision."Source" = NEW."Source"
                            AND decision."RowId" = NEW."RowId" AND decision."Finding" = NEW."Finding"
                            AND decision."EvidenceRevision" = NEW."EvidenceRevision"
                            AND NOT EXISTS (
                                SELECT 1 FROM public."AppLibraryStockVerificationDecisions" AS later
                                WHERE later."TenantId" = decision."TenantId" AND later."SessionId" = decision."SessionId"
                                    AND later."Source" = decision."Source" AND later."RowId" = decision."RowId"
                                    AND later."Version" > decision."Version")
                    ) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Published stock verification findings require their approval and current decisions.';
                    END IF;
                    IF NEW."Source" = 1 AND (
                        NEW."Finding" IN (14, 15) OR NOT EXISTS (
                            SELECT 1 FROM public."AppLibraryStockVerificationBaselines"
                            WHERE "TenantId" = NEW."TenantId" AND "SessionId" = NEW."SessionId" AND "CopyId" = NEW."RowId"
                                AND "CopyStatus" = NEW."BaselineStatus" AND "AccessionNumber" = NEW."AccessionNumber"
                        ) OR NEW."ObservationSequence" IS DISTINCT FROM (
                            SELECT max("Sequence") FROM public."AppLibraryStockVerificationObservations"
                            WHERE "TenantId" = NEW."TenantId" AND "SessionId" = NEW."SessionId" AND "CopyId" = NEW."RowId")
                    ) OR NEW."Source" IN (2, 3) AND (
                        NEW."BaselineStatus" IS NOT NULL OR NEW."CurrentStatus" IS NOT NULL OR NOT EXISTS (
                            SELECT 1 FROM public."AppLibraryStockVerificationObservations" AS scan
                            WHERE scan."TenantId" = NEW."TenantId" AND scan."SessionId" = NEW."SessionId" AND scan."Id" = NEW."RowId"
                                AND scan."Sequence" = NEW."ObservationSequence" AND scan."AccessionNumber" = NEW."AccessionNumber"
                                AND ((NEW."Source" = 3 AND NEW."Finding" = 15 AND scan."CopyId" IS NULL) OR
                                     (NEW."Source" = 2 AND NEW."Finding" = 14 AND scan."CopyId" IS NOT NULL
                                      AND NOT EXISTS (
                                          SELECT 1 FROM public."AppLibraryStockVerificationBaselines" AS baseline
                                          WHERE baseline."TenantId" = scan."TenantId" AND baseline."SessionId" = scan."SessionId"
                                              AND baseline."CopyId" = scan."CopyId")
                                      AND NOT EXISTS (
                                          SELECT 1 FROM public."AppLibraryStockVerificationObservations" AS later
                                          WHERE later."TenantId" = scan."TenantId" AND later."SessionId" = scan."SessionId"
                                              AND later."CopyId" = scan."CopyId" AND later."Sequence" > scan."Sequence")))
                        )
                    ) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Published stock verification findings require their recorded baseline or scan.';
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE TRIGGER "TR_AppLibraryStockVerificationResult_AppendOnly"
                BEFORE INSERT OR UPDATE OR DELETE ON public."AppLibraryStockVerificationResults"
                FOR EACH ROW EXECUTE FUNCTION public."AppProtectLibraryStockVerificationResult"();

                CREATE FUNCTION public."AppRequireLibraryStockVerificationResult"()
                RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    expected integer;
                    expected_rows integer;
                    outside_rows integer;
                    unknown_rows integer;
                    end_position integer;
                BEGIN
                    IF NEW."Kind" <> 6 THEN
                        RETURN NEW;
                    END IF;
                    SELECT "ExpectedCopyCount" INTO expected FROM public."AppLibraryStockVerificationSessions"
                    WHERE "TenantId" = NEW."TenantId" AND "Id" = NEW."SessionId" AND "Status" = 3
                        AND "FinalizedBy" = NEW."ActorId" AND "FinalizedAt" = NEW."RecordedAt";
                    IF NOT FOUND THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification approval requires its finalized count.';
                    END IF;
                    SELECT count(*) FILTER (WHERE "Source" = 1), count(*) FILTER (WHERE "Source" = 2),
                        count(*) FILTER (WHERE "Source" = 3), COALESCE(max("Position"), 0)
                    INTO expected_rows, outside_rows, unknown_rows, end_position
                    FROM public."AppLibraryStockVerificationResults"
                    WHERE "TenantId" = NEW."TenantId" AND "SessionId" = NEW."SessionId" AND "CommandId" = NEW."Id";
                    IF expected_rows <> expected OR end_position <> expected_rows + outside_rows + unknown_rows
                        OR outside_rows <> (
                            SELECT count(DISTINCT scan."CopyId") FROM public."AppLibraryStockVerificationObservations" AS scan
                            WHERE scan."TenantId" = NEW."TenantId" AND scan."SessionId" = NEW."SessionId" AND scan."CopyId" IS NOT NULL
                                AND NOT EXISTS (
                                    SELECT 1 FROM public."AppLibraryStockVerificationBaselines" AS baseline
                                    WHERE baseline."TenantId" = scan."TenantId" AND baseline."SessionId" = scan."SessionId"
                                        AND baseline."CopyId" = scan."CopyId"))
                        OR unknown_rows <> (
                            SELECT count(*) FROM public."AppLibraryStockVerificationObservations"
                            WHERE "TenantId" = NEW."TenantId" AND "SessionId" = NEW."SessionId" AND "CopyId" IS NULL)
                    THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification approval requires the complete published result.';
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE CONSTRAINT TRIGGER "TR_AppLibraryStockResultCommand_Outcome"
                AFTER INSERT ON public."AppLibraryStockVerificationCommands"
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION public."AppRequireLibraryStockVerificationResult"();

                CREATE FUNCTION public."AppRequireLibraryStockVerificationApproval"()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF OLD."Status" <> 3 AND NEW."Status" = 3 AND NOT EXISTS (
                        SELECT 1 FROM public."AppLibraryStockVerificationCommands"
                        WHERE "TenantId" = NEW."TenantId" AND "SessionId" = NEW."Id" AND "Kind" = 6
                            AND "ActorId" = NEW."FinalizedBy" AND "RecordedAt" = NEW."FinalizedAt"
                    ) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification finalization requires retained approval evidence.';
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE CONSTRAINT TRIGGER "TR_AppLibraryStockSession_Approval"
                AFTER UPDATE ON public."AppLibraryStockVerificationSessions"
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION public."AppRequireLibraryStockVerificationApproval"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE public."AppLibraryStockVerificationSessions", public."AppLibraryStockVerificationCommands",
                    public."AppLibraryStockVerificationResults" IN ACCESS EXCLUSIVE MODE;
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM public."AppLibraryStockVerificationResults")
                        OR EXISTS (SELECT 1 FROM public."AppLibraryStockVerificationCommands" WHERE "Kind" = 6) THEN
                        RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Stock verification rollback refused while published results exist.';
                    END IF;
                END $$;
                DROP TRIGGER "TR_AppLibraryStockSession_Approval" ON public."AppLibraryStockVerificationSessions";
                DROP TRIGGER "TR_AppLibraryStockResultCommand_Outcome" ON public."AppLibraryStockVerificationCommands";
                DROP TRIGGER "TR_AppLibraryStockVerificationResult_AppendOnly" ON public."AppLibraryStockVerificationResults";
                DROP FUNCTION public."AppRequireLibraryStockVerificationApproval"();
                DROP FUNCTION public."AppRequireLibraryStockVerificationResult"();
                DROP FUNCTION public."AppProtectLibraryStockVerificationResult"();
                """);

            migrationBuilder.DropTable(
                name: "AppLibraryStockVerificationResults");

            migrationBuilder.DropIndex(
                name: "IX_LibraryStockObservation_Tenant_Session_Copy_Sequence",
                table: "AppLibraryStockVerificationObservations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_LibraryStockDecision_Tenant_Session_Id",
                table: "AppLibraryStockVerificationDecisions");

            migrationBuilder.DropIndex(
                name: "UX_LibraryStockCommand_Tenant_Session_Transition",
                table: "AppLibraryStockVerificationCommands");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LibraryStockVerificationCommand_Target",
                table: "AppLibraryStockVerificationCommands");

            migrationBuilder.CreateIndex(
                name: "UX_LibraryStockCommand_Tenant_Session_Transition",
                table: "AppLibraryStockVerificationCommands",
                columns: new[] { "TenantId", "SessionId", "Kind" },
                unique: true,
                filter: "\"Kind\" IN (3, 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LibraryStockVerificationCommand_Target",
                table: "AppLibraryStockVerificationCommands",
                sql: "(\"Kind\" IN (1, 3, 4, 5) AND \"ObservationId\" IS NULL) OR (\"Kind\" = 2 AND \"ObservationId\" IS NOT NULL)");
        }
    }
}
