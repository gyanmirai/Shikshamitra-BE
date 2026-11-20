using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryStockResultReceiptEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReceivedAfterOpening",
                table: "AppLibraryStockVerificationResults",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_LibraryStockResult_ReceiptSource",
                table: "AppLibraryStockVerificationResults",
                sql: "NOT \"ReceivedAfterOpening\" OR \"Source\" = 2");
            migrationBuilder.Sql("""
                CREATE FUNCTION public."AppProtectLibraryStockResultReceiptEvidence"()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."ReceivedAfterOpening" AND NOT EXISTS (
                        SELECT 1
                        FROM public."AppLibraryStockVerificationSessions" AS session
                        JOIN public."AppLibraryStockVerificationObservations" AS scan
                            ON scan."TenantId" = session."TenantId" AND scan."SessionId" = session."Id"
                        JOIN public."AppLibraryReceivedCopies" AS copy
                            ON copy."TenantId" = scan."TenantId" AND copy."CopyId" = scan."CopyId"
                        JOIN public."AppLibraryStockReceiptLines" AS line
                            ON line."TenantId" = copy."TenantId" AND line."Id" = copy."ReceiptLineId"
                        JOIN public."AppLibraryStockReceipts" AS receipt
                            ON receipt."TenantId" = line."TenantId" AND receipt."Id" = line."ReceiptId"
                        JOIN public."AppLibrarySections" AS section
                            ON section."TenantId" = line."TenantId" AND section."Id" = line."SectionId"
                        WHERE session."TenantId" = NEW."TenantId" AND session."Id" = NEW."SessionId" AND scan."Id" = NEW."RowId"
                            AND (session."AcademicYearId" IS NULL OR section."AcademicYearId" = session."AcademicYearId")
                            AND receipt."RecordedAt" > session."OpenedAt" AND receipt."RecordedAt" <= scan."ObservedAt"
                    ) THEN
                        RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'Stock verification requires a receipt recorded between opening and scanning.';
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE TRIGGER "TR_AppLibraryStockResult_ReceiptEvidence"
                BEFORE INSERT ON public."AppLibraryStockVerificationResults"
                FOR EACH ROW EXECUTE FUNCTION public."AppProtectLibraryStockResultReceiptEvidence"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE public."AppLibraryStockVerificationResults" IN ACCESS EXCLUSIVE MODE;
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM public."AppLibraryStockVerificationResults" WHERE "ReceivedAfterOpening") THEN
                        RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Stock verification rollback refused while published receipt evidence exists.';
                    END IF;
                END $$;
                DROP TRIGGER "TR_AppLibraryStockResult_ReceiptEvidence" ON public."AppLibraryStockVerificationResults";
                DROP FUNCTION public."AppProtectLibraryStockResultReceiptEvidence"();
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_LibraryStockResult_ReceiptSource",
                table: "AppLibraryStockVerificationResults");

            migrationBuilder.DropColumn(
                name: "ReceivedAfterOpening",
                table: "AppLibraryStockVerificationResults");
        }
    }
}
