using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class PersistLibraryStockVerificationTransitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                sql: "(\"Kind\" IN (1, 3, 4) AND \"ObservationId\" IS NULL) OR (\"Kind\" = 2 AND \"ObservationId\" IS NOT NULL)");

            migrationBuilder.Sql("""
                CREATE FUNCTION public."AppProtectLibraryStockVerificationTransitionCommand"()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."Kind" IN (3, 4) AND NOT EXISTS (
                        SELECT 1 FROM public."AppLibraryStockVerificationSessions" AS session
                        WHERE session."TenantId" = NEW."TenantId" AND session."Id" = NEW."SessionId"
                          AND ((NEW."Kind" = 3 AND session."Status" = 2 AND session."ReviewStartedBy" = NEW."ActorId"
                                AND session."ReviewStartedAt" = NEW."RecordedAt")
                               OR (NEW."Kind" = 4 AND session."Status" = 4 AND session."CancelledBy" = NEW."ActorId"
                                AND session."CancelledAt" = NEW."RecordedAt"))
                    ) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification transition command requires its recorded outcome.';
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE CONSTRAINT TRIGGER "TR_AppLibraryStockTransitionCommand_Outcome"
                AFTER INSERT ON public."AppLibraryStockVerificationCommands"
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION public."AppProtectLibraryStockVerificationTransitionCommand"();

                CREATE FUNCTION public."AppRequireLibraryStockVerificationTransitionCommand"()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."Status" = OLD."Status" OR NEW."Status" NOT IN (2, 4) THEN
                        RETURN NEW;
                    END IF;
                    IF NEW."ObservationCount" <> OLD."ObservationCount" OR NEW."LastObservedAt" IS DISTINCT FROM OLD."LastObservedAt"
                        OR NOT EXISTS (
                            SELECT 1 FROM public."AppLibraryStockVerificationCommands" AS command
                            WHERE command."TenantId" = NEW."TenantId" AND command."SessionId" = NEW."Id"
                              AND ((NEW."Status" = 2 AND command."Kind" = 3 AND command."ActorId" = NEW."ReviewStartedBy"
                                    AND command."RecordedAt" = NEW."ReviewStartedAt")
                                   OR (NEW."Status" = 4 AND command."Kind" = 4 AND command."ActorId" = NEW."CancelledBy"
                                    AND command."RecordedAt" = NEW."CancelledAt"))
                        ) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification transition requires retained command evidence and a stable scan count.';
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE CONSTRAINT TRIGGER "TR_AppLibraryStockSession_TransitionCommand"
                AFTER UPDATE ON public."AppLibraryStockVerificationSessions"
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION public."AppRequireLibraryStockVerificationTransitionCommand"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE public."AppLibraryStockVerificationSessions", public."AppLibraryStockVerificationCommands" IN ACCESS EXCLUSIVE MODE;
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM public."AppLibraryStockVerificationCommands" WHERE "Kind" IN (3, 4)) THEN
                        RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Stock verification rollback refused while review or cancellation commands exist.';
                    END IF;
                END $$;

                DROP TRIGGER "TR_AppLibraryStockSession_TransitionCommand" ON public."AppLibraryStockVerificationSessions";
                DROP TRIGGER "TR_AppLibraryStockTransitionCommand_Outcome" ON public."AppLibraryStockVerificationCommands";
                DROP FUNCTION public."AppRequireLibraryStockVerificationTransitionCommand"();
                DROP FUNCTION public."AppProtectLibraryStockVerificationTransitionCommand"();
                """);

            migrationBuilder.DropIndex(
                name: "UX_LibraryStockCommand_Tenant_Session_Transition",
                table: "AppLibraryStockVerificationCommands");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LibraryStockVerificationCommand_Target",
                table: "AppLibraryStockVerificationCommands");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LibraryStockVerificationCommand_Target",
                table: "AppLibraryStockVerificationCommands",
                sql: "(\"Kind\" = 1 AND \"ObservationId\" IS NULL) OR (\"Kind\" = 2 AND \"ObservationId\" IS NOT NULL)");
        }
    }
}
