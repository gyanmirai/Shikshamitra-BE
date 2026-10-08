using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20261007120000_RemoveChargeBankRouting")]
public class RemoveChargeBankRouting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Finance_Charges_Finance_BankAccounts_ReceivingBankAccountId", table: "Finance_Charges");
        migrationBuilder.DropForeignKey(name: "FK_Finance_ChargeRequests_Finance_BankAccounts_ReceivingBankAc~", table: "Finance_ChargeRequests");
        migrationBuilder.DropIndex(name: "IX_Finance_Charges_ReceivingBankAccountId", table: "Finance_Charges");
        migrationBuilder.DropIndex(name: "IX_Finance_ChargeRequests_ReceivingBankAccountId", table: "Finance_ChargeRequests");
        migrationBuilder.DropColumn(name: "ReceivingBankAccountId", table: "Finance_Charges");
        migrationBuilder.DropColumn(name: "ReceivingBankLabel", table: "Finance_Charges");
        migrationBuilder.DropColumn(name: "ReceivingBankAccountId", table: "Finance_ChargeRequests");
        migrationBuilder.DropColumn(name: "ReceivingBankLabel", table: "Finance_ChargeRequests");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<System.Guid>(name: "ReceivingBankAccountId", table: "Finance_Charges", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ReceivingBankLabel", table: "Finance_Charges", type: "character varying(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<System.Guid>(name: "ReceivingBankAccountId", table: "Finance_ChargeRequests", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ReceivingBankLabel", table: "Finance_ChargeRequests", type: "character varying(500)", maxLength: 500, nullable: true);
        migrationBuilder.CreateIndex(name: "IX_Finance_Charges_ReceivingBankAccountId", table: "Finance_Charges", column: "ReceivingBankAccountId");
        migrationBuilder.CreateIndex(name: "IX_Finance_ChargeRequests_ReceivingBankAccountId", table: "Finance_ChargeRequests", column: "ReceivingBankAccountId");
        migrationBuilder.AddForeignKey(name: "FK_Finance_Charges_Finance_BankAccounts_ReceivingBankAccountId", table: "Finance_Charges", column: "ReceivingBankAccountId", principalTable: "Finance_BankAccounts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_Finance_ChargeRequests_Finance_BankAccounts_ReceivingBankAc~", table: "Finance_ChargeRequests", column: "ReceivingBankAccountId", principalTable: "Finance_BankAccounts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }
}
