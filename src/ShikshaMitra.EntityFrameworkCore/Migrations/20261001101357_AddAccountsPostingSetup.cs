using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountsPostingSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Finance_AccountingPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    IsOpen = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_AccountingPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Finance_AccountsPostingConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstPostingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CashAccountHeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_AccountsPostingConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_AccountsPostingConfigurations_Finance_AccountHeads_~",
                        column: x => x.CashAccountHeadId,
                        principalTable: "Finance_AccountHeads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Finance_PaymentDestinationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    AssetAccountHeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_PaymentDestinationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_PaymentDestinationRules_Finance_AccountHeads_AssetA~",
                        column: x => x.AssetAccountHeadId,
                        principalTable: "Finance_AccountHeads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Finance_PaymentDestinationRules_Finance_BankAccounts_BankAc~",
                        column: x => x.BankAccountId,
                        principalTable: "Finance_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_JournalEntries_AccountingPeriodId",
                table: "Finance_JournalEntries",
                column: "AccountingPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_AccountingPeriods_TenantId_Code",
                table: "Finance_AccountingPeriods",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_AccountingPeriods_TenantId_StartsOn_EndsOn",
                table: "Finance_AccountingPeriods",
                columns: new[] { "TenantId", "StartsOn", "EndsOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_AccountsPostingConfigurations_CashAccountHeadId",
                table: "Finance_AccountsPostingConfigurations",
                column: "CashAccountHeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_AccountsPostingConfigurations_TenantId_AcademicYear~",
                table: "Finance_AccountsPostingConfigurations",
                columns: new[] { "TenantId", "AcademicYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_PaymentDestinationRules_AssetAccountHeadId",
                table: "Finance_PaymentDestinationRules",
                column: "AssetAccountHeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_PaymentDestinationRules_BankAccountId",
                table: "Finance_PaymentDestinationRules",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_PaymentDestinationRules_TenantId_AcademicYearId_Dir~",
                table: "Finance_PaymentDestinationRules",
                columns: new[] { "TenantId", "AcademicYearId", "Direction", "Method", "EffectiveFrom" });

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_JournalEntries_Finance_AccountingPeriods_Accounting~",
                table: "Finance_JournalEntries",
                column: "AccountingPeriodId",
                principalTable: "Finance_AccountingPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Finance_JournalEntries_Finance_AccountingPeriods_Accounting~",
                table: "Finance_JournalEntries");

            migrationBuilder.DropTable(
                name: "Finance_AccountingPeriods");

            migrationBuilder.DropTable(
                name: "Finance_AccountsPostingConfigurations");

            migrationBuilder.DropTable(
                name: "Finance_PaymentDestinationRules");

            migrationBuilder.DropIndex(
                name: "IX_Finance_JournalEntries_AccountingPeriodId",
                table: "Finance_JournalEntries");
        }
    }
}
