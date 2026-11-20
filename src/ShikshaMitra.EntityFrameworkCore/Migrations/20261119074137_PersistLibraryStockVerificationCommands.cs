using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class PersistLibraryStockVerificationCommands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_LibraryStockVerificationSession_Location",
                table: "AppLibraryStockVerificationSessions");

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicYearId",
                table: "AppLibraryStockVerificationSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_LibraryStockObservation_Tenant_Session_Id",
                table: "AppLibraryStockVerificationObservations",
                columns: new[] { "TenantId", "SessionId", "Id" });

            migrationBuilder.CreateTable(
                name: "AppLibraryStockVerificationCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryStockVerificationCommands", x => x.Id);
                    table.CheckConstraint("CK_LibraryStockVerificationCommand_Target", "(\"Kind\" = 1 AND \"ObservationId\" IS NULL) OR (\"Kind\" = 2 AND \"ObservationId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_LibraryStockCommand_Observation",
                        columns: x => new { x.TenantId, x.SessionId, x.ObservationId },
                        principalTable: "AppLibraryStockVerificationObservations",
                        principalColumns: new[] { "TenantId", "SessionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LibraryStockCommand_Session",
                        columns: x => new { x.TenantId, x.SessionId },
                        principalTable: "AppLibraryStockVerificationSessions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_LibraryStockVerificationSession_Location",
                table: "AppLibraryStockVerificationSessions",
                sql: "\"SectionId\" IS NOT NULL OR (\"ShelfId\" IS NULL AND \"AcademicYearId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryStockVerificationCommands_TenantId_SessionId_Obse~",
                table: "AppLibraryStockVerificationCommands",
                columns: new[] { "TenantId", "SessionId", "ObservationId" });

            migrationBuilder.CreateIndex(
                name: "UX_LibraryStockCommand_Tenant_Actor_Key",
                table: "AppLibraryStockVerificationCommands",
                columns: new[] { "TenantId", "ActorId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.Sql("""
                CREATE FUNCTION public."AppProtectLibraryStockVerificationCommand"()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification command evidence is append-only.';
                    END IF;
                    IF NEW."IdempotencyKey" = '' OR btrim(NEW."IdempotencyKey") <> NEW."IdempotencyKey"
                        OR NEW."IdempotencyKey" ~ '[[:cntrl:]]' OR NEW."PayloadFingerprint" !~ '^[0-9a-fA-F]{64}$' THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification command identity is invalid.';
                    END IF;
                    IF NEW."Kind" = 1 AND NOT EXISTS (
                        SELECT 1 FROM public."AppLibraryStockVerificationSessions"
                        WHERE "TenantId" = NEW."TenantId" AND "Id" = NEW."SessionId" AND
                            "OpenedBy" = NEW."ActorId" AND "OpenedAt" = NEW."RecordedAt"
                    ) OR NEW."Kind" = 2 AND NOT EXISTS (
                        SELECT 1 FROM public."AppLibraryStockVerificationObservations"
                        WHERE "TenantId" = NEW."TenantId" AND "SessionId" = NEW."SessionId" AND "Id" = NEW."ObservationId" AND
                            "ObservedBy" = NEW."ActorId" AND "ObservedAt" = NEW."RecordedAt"
                    ) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification command requires its recorded actor and outcome.';
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE TRIGGER "TR_AppLibraryStockVerificationCommand_AppendOnly"
                BEFORE INSERT OR UPDATE OR DELETE ON public."AppLibraryStockVerificationCommands"
                FOR EACH ROW EXECUTE FUNCTION public."AppProtectLibraryStockVerificationCommand"();

                CREATE FUNCTION public."AppProtectLibraryStockVerificationYear"()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF OLD."AcademicYearId" IS DISTINCT FROM NEW."AcademicYearId" THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'The recorded stock verification year is immutable.';
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE TRIGGER "TR_AppLibraryStockVerificationSession_Year"
                BEFORE UPDATE OF "AcademicYearId" ON public."AppLibraryStockVerificationSessions"
                FOR EACH ROW EXECUTE FUNCTION public."AppProtectLibraryStockVerificationYear"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE public."AppLibraryStockVerificationSessions", public."AppLibraryStockVerificationCommands" IN ACCESS EXCLUSIVE MODE;
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM public."AppLibraryStockVerificationCommands") OR
                        EXISTS (SELECT 1 FROM public."AppLibraryStockVerificationSessions" WHERE "AcademicYearId" IS NOT NULL) THEN
                        RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Stock verification command rollback refused while replay or scope evidence exists.';
                    END IF;
                END $$;
                DROP TRIGGER "TR_AppLibraryStockVerificationSession_Year" ON public."AppLibraryStockVerificationSessions";
                DROP FUNCTION public."AppProtectLibraryStockVerificationYear"();
                """);

            migrationBuilder.DropTable(
                name: "AppLibraryStockVerificationCommands");

            migrationBuilder.Sql("DROP FUNCTION public.\"AppProtectLibraryStockVerificationCommand\"();");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LibraryStockVerificationSession_Location",
                table: "AppLibraryStockVerificationSessions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_LibraryStockObservation_Tenant_Session_Id",
                table: "AppLibraryStockVerificationObservations");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "AppLibraryStockVerificationSessions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LibraryStockVerificationSession_Location",
                table: "AppLibraryStockVerificationSessions",
                sql: "\"ShelfId\" IS NULL OR \"SectionId\" IS NOT NULL");
        }
    }
}
