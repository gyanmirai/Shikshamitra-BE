using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryCopyLifecycleEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppLibraryCopyEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BookId = table.Column<Guid>(type: "uuid", nullable: false),
                    CopyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PreviousStatus = table.Column<int>(type: "integer", nullable: false),
                    NextStatus = table.Column<int>(type: "integer", nullable: false),
                    PreviousRemarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NextRemarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FromSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromShelfId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToSectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToShelfId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelatedLoanId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryCopyEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLibraryCopyEvents_AppBookCopies_CopyId",
                        column: x => x.CopyId,
                        principalTable: "AppBookCopies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryCopyEvents_AppBookIssues_RelatedLoanId",
                        column: x => x.RelatedLoanId,
                        principalTable: "AppBookIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppLibraryCopyEvents_AppBooks_BookId",
                        column: x => x.BookId,
                        principalTable: "AppBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCopyEvents_BookId",
                table: "AppLibraryCopyEvents",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCopyEvents_CopyId",
                table: "AppLibraryCopyEvents",
                column: "CopyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCopyEvents_RelatedLoanId",
                table: "AppLibraryCopyEvents",
                column: "RelatedLoanId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCopyEvents_TenantId_CopyId_Sequence",
                table: "AppLibraryCopyEvents",
                columns: new[] { "TenantId", "CopyId", "Sequence" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.Sql("""
                CREATE FUNCTION "AppLibraryCopyEvents_BlockMutation"() RETURNS trigger
                LANGUAGE plpgsql AS $library_history$
                BEGIN
                    RAISE EXCEPTION USING ERRCODE = 'P0001',
                        MESSAGE = 'Library copy history is append-only; record a new event instead.';
                END;
                $library_history$;
                CREATE TRIGGER "TR_AppLibraryCopyEvents_ImmutableRows"
                    BEFORE UPDATE OR DELETE ON "AppLibraryCopyEvents"
                    FOR EACH ROW EXECUTE FUNCTION "AppLibraryCopyEvents_BlockMutation"();
                CREATE TRIGGER "TR_AppLibraryCopyEvents_NoTruncate"
                    BEFORE TRUNCATE ON "AppLibraryCopyEvents"
                    FOR EACH STATEMENT EXECUTE FUNCTION "AppLibraryCopyEvents_BlockMutation"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE "AppLibraryCopyEvents" IN ACCESS EXCLUSIVE MODE;
                DO $library_history$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "AppLibraryCopyEvents") THEN
                        RAISE EXCEPTION 'Library lifecycle rollback cannot preserve existing copy history. Retain the schema and reconcile the recorded evidence before rollback.';
                    END IF;
                END;
                $library_history$;
                """);
            migrationBuilder.DropTable(
                name: "AppLibraryCopyEvents");
            migrationBuilder.Sql("DROP FUNCTION \"AppLibraryCopyEvents_BlockMutation\"();");
        }
    }
}
