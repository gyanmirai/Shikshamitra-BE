using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountsPostingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Finance_FinancialEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Transition = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TransitionVersion = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    SnapshotHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_FinancialEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Finance_JournalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinancialEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountingPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrectsJournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    JournalNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PostedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_JournalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_JournalEntries_Finance_FinancialEvents_FinancialEve~",
                        column: x => x.FinancialEventId,
                        principalTable: "Finance_FinancialEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Finance_JournalEntries_Finance_JournalEntries_CorrectsJourn~",
                        column: x => x.CorrectsJournalEntryId,
                        principalTable: "Finance_JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Finance_JournalLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountHeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceAllocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Debit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_JournalLines", x => x.Id);
                    table.CheckConstraint("CK_Finance_JournalLines_OneSide", "(\"Debit\" > 0 AND \"Credit\" = 0) OR (\"Credit\" > 0 AND \"Debit\" = 0)");
                    table.ForeignKey(
                        name: "FK_Finance_JournalLines_Finance_AccountHeads_AccountHeadId",
                        column: x => x.AccountHeadId,
                        principalTable: "Finance_AccountHeads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Finance_JournalLines_Finance_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "Finance_JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FinancialEvents_EventKey",
                table: "Finance_FinancialEvents",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FinancialEvents_TenantId_SourceType_SourceId",
                table: "Finance_FinancialEvents",
                columns: new[] { "TenantId", "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_JournalEntries_CorrectsJournalEntryId",
                table: "Finance_JournalEntries",
                column: "CorrectsJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_JournalEntries_FinancialEventId",
                table: "Finance_JournalEntries",
                column: "FinancialEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_JournalEntries_JournalNumber",
                table: "Finance_JournalEntries",
                column: "JournalNumber",
                unique: true,
                filter: "\"TenantId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_JournalEntries_TenantId_JournalNumber",
                table: "Finance_JournalEntries",
                columns: new[] { "TenantId", "JournalNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_JournalLines_AccountHeadId",
                table: "Finance_JournalLines",
                column: "AccountHeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_JournalLines_JournalEntryId",
                table: "Finance_JournalLines",
                column: "JournalEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Finance_JournalLines");

            migrationBuilder.DropTable(
                name: "Finance_JournalEntries");

            migrationBuilder.DropTable(
                name: "Finance_FinancialEvents");
        }
    }
}
