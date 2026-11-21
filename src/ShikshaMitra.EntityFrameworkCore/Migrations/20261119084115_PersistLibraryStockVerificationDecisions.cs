using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class PersistLibraryStockVerificationDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_LibraryStockVerificationCommand_Target",
                table: "AppLibraryStockVerificationCommands");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_LibraryStockCommand_Tenant_Session_Id",
                table: "AppLibraryStockVerificationCommands",
                columns: new[] { "TenantId", "SessionId", "Id" });

            migrationBuilder.CreateTable(
                name: "AppLibraryStockVerificationDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    RowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    AccessionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Finding = table.Column<int>(type: "integer", nullable: false),
                    EvidenceRevision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryStockVerificationDecisions", x => x.Id);
                    table.CheckConstraint("CK_LibraryStockDecision_Outcome", "\"Outcome\" IN (1, 2, 3)");
                    table.CheckConstraint("CK_LibraryStockDecision_Source", "\"Source\" IN (1, 2, 3)");
                    table.CheckConstraint("CK_LibraryStockDecision_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_LibraryStockDecision_Command",
                        columns: x => new { x.TenantId, x.SessionId, x.CommandId },
                        principalTable: "AppLibraryStockVerificationCommands",
                        principalColumns: new[] { "TenantId", "SessionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_LibraryStockVerificationCommand_Target",
                table: "AppLibraryStockVerificationCommands",
                sql: "(\"Kind\" IN (1, 3, 4, 5) AND \"ObservationId\" IS NULL) OR (\"Kind\" = 2 AND \"ObservationId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "UX_LibraryStockDecision_Tenant_Session_Command",
                table: "AppLibraryStockVerificationDecisions",
                columns: new[] { "TenantId", "SessionId", "CommandId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_LibraryStockDecision_Tenant_Session_Row_Version",
                table: "AppLibraryStockVerificationDecisions",
                columns: new[] { "TenantId", "SessionId", "Source", "RowId", "Version" },
                unique: true);

            migrationBuilder.Sql("""
                CREATE FUNCTION public."AppProtectLibraryStockVerificationDecision"()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification decisions are append-only.';
                    END IF;
                    IF NEW."EvidenceRevision" !~ '^[0-9a-fA-F]{64}$' OR NEW."Reason" ~ '^[[:space:]]*$'
                        OR NEW."Version" <> 1 + COALESCE((
                            SELECT max("Version") FROM public."AppLibraryStockVerificationDecisions"
                            WHERE "TenantId" = NEW."TenantId" AND "SessionId" = NEW."SessionId"
                                AND "Source" = NEW."Source" AND "RowId" = NEW."RowId"), 0)
                        OR NOT EXISTS (
                            SELECT 1 FROM public."AppLibraryStockVerificationSessions" AS session
                            JOIN public."AppLibraryStockVerificationCommands" AS command
                              ON command."TenantId" = session."TenantId" AND command."SessionId" = session."Id"
                            WHERE session."TenantId" = NEW."TenantId" AND session."Id" = NEW."SessionId"
                                AND session."Status" = 2 AND NEW."DecidedAt" >= session."ReviewStartedAt"
                                AND command."Id" = NEW."CommandId" AND command."Kind" = 5
                                AND command."ActorId" = NEW."DecidedBy" AND command."RecordedAt" = NEW."DecidedAt"
                            FOR SHARE OF session
                        ) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification decision requires an active review and its matching command.';
                    END IF;
                    IF NEW."Source" = 1 AND (NEW."Finding" NOT IN (2, 3, 4, 10, 12, 13) OR NOT EXISTS (
                        SELECT 1 FROM public."AppLibraryStockVerificationBaselines"
                        WHERE "TenantId" = NEW."TenantId" AND "SessionId" = NEW."SessionId"
                            AND "CopyId" = NEW."RowId" AND "AccessionNumber" = NEW."AccessionNumber"
                    )) OR NEW."Source" IN (2, 3) AND NOT EXISTS (
                        SELECT 1 FROM public."AppLibraryStockVerificationObservations" AS scan
                        WHERE scan."TenantId" = NEW."TenantId" AND scan."SessionId" = NEW."SessionId"
                            AND scan."Id" = NEW."RowId" AND scan."AccessionNumber" = NEW."AccessionNumber"
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
                    ) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification decision requires its recorded finding.';
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE TRIGGER "TR_AppLibraryStockVerificationDecision_AppendOnly"
                BEFORE INSERT OR UPDATE OR DELETE ON public."AppLibraryStockVerificationDecisions"
                FOR EACH ROW EXECUTE FUNCTION public."AppProtectLibraryStockVerificationDecision"();

                CREATE FUNCTION public."AppRequireLibraryStockVerificationDecision"()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."Kind" = 5 AND NOT EXISTS (
                        SELECT 1 FROM public."AppLibraryStockVerificationDecisions"
                        WHERE "TenantId" = NEW."TenantId" AND "SessionId" = NEW."SessionId" AND "CommandId" = NEW."Id"
                    ) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification decision command requires a retained finding decision.';
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE CONSTRAINT TRIGGER "TR_AppLibraryStockDecisionCommand_Outcome"
                AFTER INSERT ON public."AppLibraryStockVerificationCommands"
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION public."AppRequireLibraryStockVerificationDecision"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE public."AppLibraryStockVerificationCommands", public."AppLibraryStockVerificationDecisions" IN ACCESS EXCLUSIVE MODE;
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM public."AppLibraryStockVerificationDecisions")
                        OR EXISTS (SELECT 1 FROM public."AppLibraryStockVerificationCommands" WHERE "Kind" = 5) THEN
                        RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Stock verification rollback refused while retained review decisions exist.';
                    END IF;
                END $$;
                DROP TRIGGER "TR_AppLibraryStockDecisionCommand_Outcome" ON public."AppLibraryStockVerificationCommands";
                DROP TRIGGER "TR_AppLibraryStockVerificationDecision_AppendOnly" ON public."AppLibraryStockVerificationDecisions";
                DROP FUNCTION public."AppRequireLibraryStockVerificationDecision"();
                DROP FUNCTION public."AppProtectLibraryStockVerificationDecision"();
                """);

            migrationBuilder.DropTable(
                name: "AppLibraryStockVerificationDecisions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_LibraryStockCommand_Tenant_Session_Id",
                table: "AppLibraryStockVerificationCommands");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LibraryStockVerificationCommand_Target",
                table: "AppLibraryStockVerificationCommands");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LibraryStockVerificationCommand_Target",
                table: "AppLibraryStockVerificationCommands",
                sql: "(\"Kind\" IN (1, 3, 4) AND \"ObservationId\" IS NULL) OR (\"Kind\" = 2 AND \"ObservationId\" IS NOT NULL)");
        }
    }
}
