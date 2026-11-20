using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class PersistLibraryStockVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppLibraryStockVerificationSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShelfId = table.Column<Guid>(type: "uuid", nullable: true),
                    SchoolDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedCopyCount = table.Column<int>(type: "integer", nullable: false),
                    ObservationCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OpenedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewStartedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinalizedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinalizedEvidenceFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryStockVerificationSessions", x => x.Id);
                    table.UniqueConstraint("AK_LibraryStockVerificationSession_Tenant_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_LibraryStockVerificationSession_Counts", "\"ExpectedCopyCount\" >= 0 AND \"ObservationCount\" >= 0");
                    table.CheckConstraint("CK_LibraryStockVerificationSession_Location", "\"ShelfId\" IS NULL OR \"SectionId\" IS NOT NULL");
                    table.CheckConstraint("CK_LibraryStockVerificationSession_State", "(\"ReviewStartedBy\" IS NULL) = (\"ReviewStartedAt\" IS NULL) AND\n(\"FinalizedBy\" IS NULL) = (\"FinalizedAt\" IS NULL) AND\n(\"FinalizedBy\" IS NULL) = (\"FinalizedEvidenceFingerprint\" IS NULL) AND\n(\"CancelledBy\" IS NULL) = (\"CancelledAt\" IS NULL) AND\n(\"CancelledBy\" IS NULL) = (\"CancellationReason\" IS NULL) AND\n((\"Status\" = 1 AND \"ReviewStartedBy\" IS NULL AND \"FinalizedBy\" IS NULL AND \"CancelledBy\" IS NULL) OR\n (\"Status\" = 2 AND \"ReviewStartedBy\" IS NOT NULL AND \"FinalizedBy\" IS NULL AND \"CancelledBy\" IS NULL) OR\n (\"Status\" = 3 AND \"ReviewStartedBy\" IS NOT NULL AND \"FinalizedBy\" IS NOT NULL AND \"CancelledBy\" IS NULL) OR\n (\"Status\" = 4 AND \"FinalizedBy\" IS NULL AND \"CancelledBy\" IS NOT NULL))");
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryStockVerificationBaselines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CopyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShelfId = table.Column<Guid>(type: "uuid", nullable: false),
                    CopyStatus = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryStockVerificationBaselines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LibraryStockVerificationBaseline_Session",
                        columns: x => new { x.TenantId, x.SessionId },
                        principalTable: "AppLibraryStockVerificationSessions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryStockVerificationObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    AccessionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CopyId = table.Column<Guid>(type: "uuid", nullable: true),
                    ObservedSectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ObservedShelfId = table.Column<Guid>(type: "uuid", nullable: true),
                    SchoolDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ObservedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryStockVerificationObservations", x => x.Id);
                    table.CheckConstraint("CK_LibraryStockVerificationObservation_Location", "\"ObservedShelfId\" IS NULL OR \"ObservedSectionId\" IS NOT NULL");
                    table.CheckConstraint("CK_LibraryStockVerificationObservation_Sequence", "\"Sequence\" > 0");
                    table.ForeignKey(
                        name: "FK_LibraryStockVerificationObservation_Session",
                        columns: x => new { x.TenantId, x.SessionId },
                        principalTable: "AppLibraryStockVerificationSessions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_LibraryStockVerificationBaseline_Tenant_Session_Copy",
                table: "AppLibraryStockVerificationBaselines",
                columns: new[] { "TenantId", "SessionId", "CopyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_LibraryStockVerificationObservation_Tenant_Session_Sequence",
                table: "AppLibraryStockVerificationObservations",
                columns: new[] { "TenantId", "SessionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LibraryStockVerificationSession_Tenant_Date",
                table: "AppLibraryStockVerificationSessions",
                columns: new[] { "TenantId", "SchoolDate", "Id" });

            migrationBuilder.Sql("""
                CREATE FUNCTION public."AppProtectLibraryStockVerificationSession"()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification sessions are retained.';
                    END IF;
                    IF TG_OP = 'INSERT' THEN
                        IF NEW."Status" <> 1 OR NEW."ObservationCount" <> 0 OR NEW."IsDeleted" THEN
                            RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification starts in counting stage.';
                        END IF;
                        RETURN NEW;
                    END IF;
                    IF OLD."Status" IN (3, 4)
                        OR ROW(NEW."Id", NEW."TenantId", NEW."Name", NEW."SectionId", NEW."ShelfId", NEW."SchoolDate",
                               NEW."ExpectedCopyCount", NEW."OpenedBy", NEW."OpenedAt", NEW."IsDeleted")
                            IS DISTINCT FROM
                           ROW(OLD."Id", OLD."TenantId", OLD."Name", OLD."SectionId", OLD."ShelfId", OLD."SchoolDate",
                               OLD."ExpectedCopyCount", OLD."OpenedBy", OLD."OpenedAt", OLD."IsDeleted") THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification baseline and closed sessions are sealed.';
                    END IF;
                    IF (OLD."Status" = 1 AND NEW."Status" = 3) OR (OLD."Status" = 2 AND NEW."Status" = 1)
                        OR NEW."ObservationCount" < OLD."ObservationCount"
                        OR (OLD."Status" = 2 AND ROW(NEW."ReviewStartedBy", NEW."ReviewStartedAt", NEW."ObservationCount", NEW."LastObservedAt")
                            IS DISTINCT FROM ROW(OLD."ReviewStartedBy", OLD."ReviewStartedAt", OLD."ObservationCount", OLD."LastObservedAt"))
                        OR (NEW."ObservationCount" > 0 AND NEW."LastObservedAt" IS NULL)
                        OR NEW."LastObservedAt" < COALESCE(OLD."LastObservedAt", OLD."OpenedAt")
                        OR NEW."ReviewStartedAt" < COALESCE(NEW."LastObservedAt", NEW."OpenedAt")
                        OR NEW."FinalizedAt" < NEW."ReviewStartedAt"
                        OR NEW."CancelledAt" < COALESCE(NEW."ReviewStartedAt", NEW."LastObservedAt", NEW."OpenedAt")
                        OR (NEW."Status" = 3 AND NEW."FinalizedEvidenceFingerprint" !~ '^[0-9a-fA-F]{64}$') THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification transition requires current ordered evidence.';
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE TRIGGER "TR_AppLibraryStockVerificationSession_Retain"
                BEFORE INSERT OR UPDATE OR DELETE ON public."AppLibraryStockVerificationSessions"
                FOR EACH ROW EXECUTE FUNCTION public."AppProtectLibraryStockVerificationSession"();

                CREATE FUNCTION public."AppProtectLibraryStockVerificationEvidence"()
                RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    session_row public."AppLibraryStockVerificationSessions"%ROWTYPE;
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification evidence is append-only.';
                    END IF;
                    SELECT * INTO session_row FROM public."AppLibraryStockVerificationSessions"
                        WHERE "TenantId" = NEW."TenantId" AND "Id" = NEW."SessionId" FOR SHARE;
                    IF NOT FOUND THEN
                        RETURN NEW;
                    END IF;
                    IF session_row."Status" <> 1 THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification counting is closed.';
                    END IF;
                    IF TG_TABLE_NAME = 'AppLibraryStockVerificationBaselines' THEN
                        IF session_row."ObservationCount" <> 0
                            OR (session_row."SectionId" IS NOT NULL AND session_row."SectionId" <> NEW."SectionId")
                            OR (session_row."ShelfId" IS NOT NULL AND session_row."ShelfId" <> NEW."ShelfId") THEN
                            RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification baseline is outside its recorded scope or has started counting.';
                        END IF;
                    ELSE
                        IF NEW."SchoolDate" < session_row."SchoolDate" OR NEW."ObservedAt" < session_row."OpenedAt"
                            OR btrim(NEW."AccessionNumber") = '' THEN
                            RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification observation requires a recorded accession and date.';
                        END IF;
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE TRIGGER "TR_AppLibraryStockVerificationBaseline_AppendOnly"
                BEFORE INSERT OR UPDATE OR DELETE ON public."AppLibraryStockVerificationBaselines"
                FOR EACH ROW EXECUTE FUNCTION public."AppProtectLibraryStockVerificationEvidence"();

                CREATE TRIGGER "TR_AppLibraryStockVerificationObservation_AppendOnly"
                BEFORE INSERT OR UPDATE OR DELETE ON public."AppLibraryStockVerificationObservations"
                FOR EACH ROW EXECUTE FUNCTION public."AppProtectLibraryStockVerificationEvidence"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE public."AppLibraryStockVerificationSessions", public."AppLibraryStockVerificationBaselines",
                    public."AppLibraryStockVerificationObservations" IN ACCESS EXCLUSIVE MODE;
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM public."AppLibraryStockVerificationSessions") THEN
                        RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Stock verification rollback refused while retained sessions exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "AppLibraryStockVerificationBaselines");

            migrationBuilder.DropTable(
                name: "AppLibraryStockVerificationObservations");

            migrationBuilder.DropTable(
                name: "AppLibraryStockVerificationSessions");

            migrationBuilder.Sql("""
                DROP FUNCTION public."AppProtectLibraryStockVerificationEvidence"();
                DROP FUNCTION public."AppProtectLibraryStockVerificationSession"();
                """);
        }
    }
}
