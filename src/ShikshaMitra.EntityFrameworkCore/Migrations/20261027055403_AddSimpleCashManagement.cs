using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddSimpleCashManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CashCollectionId",
                table: "Finance_Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Finance_CashCollections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentInstrumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IncomeSourceKeyPrefix = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IncomeTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayerName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinanceFeeAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CollectedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SpentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DepositedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InTransitAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankLabel = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_Finance_CashCollections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_CashCollections_Finance_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "Finance_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Finance_CashCollections_Finance_FeeAccounts_FinanceFeeAccou~",
                        column: x => x.FinanceFeeAccountId,
                        principalTable: "Finance_FeeAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Finance_CashCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CashCollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_CashCommands", x => new { x.CashCollectionId, x.Id });
                    table.ForeignKey(
                        name: "FK_Finance_CashCommands_Finance_CashCollections_CashCollection~",
                        column: x => x.CashCollectionId,
                        principalTable: "Finance_CashCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Finance_CashDeposits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreditedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DepositDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreditDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CashCollectionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_CashDeposits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_CashDeposits_Finance_CashCollections_CashCollection~",
                        column: x => x.CashCollectionId,
                        principalTable: "Finance_CashCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Finance_CashExpenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CashCollectionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_CashExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_CashExpenses_Finance_CashCollections_CashCollection~",
                        column: x => x.CashCollectionId,
                        principalTable: "Finance_CashCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Finance_CashDepositCredits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreditDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CashDepositId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_CashDepositCredits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_CashDepositCredits_Finance_CashDeposits_CashDeposit~",
                        column: x => x.CashDepositId,
                        principalTable: "Finance_CashDeposits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_Transactions_CashCollectionId",
                table: "Finance_Transactions",
                column: "CashCollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_CashCollections_BankAccountId",
                table: "Finance_CashCollections",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_CashCollections_FinanceFeeAccountId",
                table: "Finance_CashCollections",
                column: "FinanceFeeAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_CashCollections_TenantId_AcademicYearId_Status",
                table: "Finance_CashCollections",
                columns: new[] { "TenantId", "AcademicYearId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_CashCollections_TenantId_IncomeSourceKeyPrefix",
                table: "Finance_CashCollections",
                columns: new[] { "TenantId", "IncomeSourceKeyPrefix" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_CashDepositCredits_CashDepositId",
                table: "Finance_CashDepositCredits",
                column: "CashDepositId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_CashDeposits_CashCollectionId",
                table: "Finance_CashDeposits",
                column: "CashCollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_CashExpenses_CashCollectionId",
                table: "Finance_CashExpenses",
                column: "CashCollectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_Transactions_Finance_CashCollections_CashCollection~",
                table: "Finance_Transactions",
                column: "CashCollectionId",
                principalTable: "Finance_CashCollections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Finance_Transactions_Finance_CashCollections_CashCollection~",
                table: "Finance_Transactions");

            migrationBuilder.DropTable(
                name: "Finance_CashCommands");

            migrationBuilder.DropTable(
                name: "Finance_CashDepositCredits");

            migrationBuilder.DropTable(
                name: "Finance_CashExpenses");

            migrationBuilder.DropTable(
                name: "Finance_CashDeposits");

            migrationBuilder.DropTable(
                name: "Finance_CashCollections");

            migrationBuilder.DropIndex(
                name: "IX_Finance_Transactions_CashCollectionId",
                table: "Finance_Transactions");

            migrationBuilder.DropColumn(
                name: "CashCollectionId",
                table: "Finance_Transactions");
        }
    }
}
