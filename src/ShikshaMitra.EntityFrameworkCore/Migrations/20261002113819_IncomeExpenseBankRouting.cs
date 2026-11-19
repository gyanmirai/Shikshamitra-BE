using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class IncomeExpenseBankRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BankAccountId",
                table: "Finance_Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankLabelSnapshot",
                table: "Finance_Transactions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoryNameSnapshot",
                table: "Finance_Transactions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsConfirmed",
                table: "Finance_Transactions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceId",
                table: "Finance_Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceKey",
                table: "Finance_Transactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StudentId",
                table: "Finance_Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultPayingBankAccountId",
                table: "Finance_RefundPolicies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExpenseCategoryId",
                table: "Finance_RefundPolicies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BankAccountId",
                table: "Finance_PaymentMethodRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankLabelSnapshot",
                table: "Finance_PaymentMethodRecords",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExpenseCategoryId",
                table: "Finance_FeeRefundRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpenseCategoryName",
                table: "Finance_FeeRefundRequests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PayingBankAccountId",
                table: "Finance_FeeRefundRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayingBankLabel",
                table: "Finance_FeeRefundRequests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LateFeeIncomeCategoryId",
                table: "Finance_CollectionPolicies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IncomeCategoryName",
                table: "Finance_Charges",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReceivingBankAccountId",
                table: "Finance_Charges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceivingBankLabel",
                table: "Finance_Charges",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IncomeCategoryId",
                table: "Finance_ChargeRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IncomeCategoryName",
                table: "Finance_ChargeRequests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReceivingBankAccountId",
                table: "Finance_ChargeRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceivingBankLabel",
                table: "Finance_ChargeRequests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(name: "IX_Finance_Transactions_SourceId", table: "Finance_Transactions", column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_Transactions_BankAccountId",
                table: "Finance_Transactions",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_Transactions_SourceKey",
                table: "Finance_Transactions",
                column: "SourceKey",
                unique: true,
                filter: "\"SourceKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_RefundPolicies_DefaultPayingBankAccountId",
                table: "Finance_RefundPolicies",
                column: "DefaultPayingBankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_RefundPolicies_ExpenseCategoryId",
                table: "Finance_RefundPolicies",
                column: "ExpenseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_PaymentMethodRecords_BankAccountId",
                table: "Finance_PaymentMethodRecords",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeRefundRequests_ExpenseCategoryId",
                table: "Finance_FeeRefundRequests",
                column: "ExpenseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeRefundRequests_PayingBankAccountId",
                table: "Finance_FeeRefundRequests",
                column: "PayingBankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_CollectionPolicies_LateFeeIncomeCategoryId",
                table: "Finance_CollectionPolicies",
                column: "LateFeeIncomeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_Charges_ReceivingBankAccountId",
                table: "Finance_Charges",
                column: "ReceivingBankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ChargeRequests_IncomeCategoryId",
                table: "Finance_ChargeRequests",
                column: "IncomeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ChargeRequests_ReceivingBankAccountId",
                table: "Finance_ChargeRequests",
                column: "ReceivingBankAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_ChargeRequests_Finance_BankAccounts_ReceivingBankAc~",
                table: "Finance_ChargeRequests",
                column: "ReceivingBankAccountId",
                principalTable: "Finance_BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_ChargeRequests_Finance_TransactionCategories_Income~",
                table: "Finance_ChargeRequests",
                column: "IncomeCategoryId",
                principalTable: "Finance_TransactionCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_Charges_Finance_BankAccounts_ReceivingBankAccountId",
                table: "Finance_Charges",
                column: "ReceivingBankAccountId",
                principalTable: "Finance_BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_CollectionPolicies_Finance_TransactionCategories_La~",
                table: "Finance_CollectionPolicies",
                column: "LateFeeIncomeCategoryId",
                principalTable: "Finance_TransactionCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeRefundRequests_Finance_BankAccounts_PayingBankAc~",
                table: "Finance_FeeRefundRequests",
                column: "PayingBankAccountId",
                principalTable: "Finance_BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeRefundRequests_Finance_TransactionCategories_Exp~",
                table: "Finance_FeeRefundRequests",
                column: "ExpenseCategoryId",
                principalTable: "Finance_TransactionCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_PaymentMethodRecords_Finance_BankAccounts_BankAccou~",
                table: "Finance_PaymentMethodRecords",
                column: "BankAccountId",
                principalTable: "Finance_BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_RefundPolicies_Finance_BankAccounts_DefaultPayingBa~",
                table: "Finance_RefundPolicies",
                column: "DefaultPayingBankAccountId",
                principalTable: "Finance_BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_RefundPolicies_Finance_TransactionCategories_Expens~",
                table: "Finance_RefundPolicies",
                column: "ExpenseCategoryId",
                principalTable: "Finance_TransactionCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_Transactions_Finance_BankAccounts_BankAccountId",
                table: "Finance_Transactions",
                column: "BankAccountId",
                principalTable: "Finance_BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Finance_Transactions_SourceId", table: "Finance_Transactions");
            migrationBuilder.DropForeignKey(
                name: "FK_Finance_ChargeRequests_Finance_BankAccounts_ReceivingBankAc~",
                table: "Finance_ChargeRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_ChargeRequests_Finance_TransactionCategories_Income~",
                table: "Finance_ChargeRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_Charges_Finance_BankAccounts_ReceivingBankAccountId",
                table: "Finance_Charges");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_CollectionPolicies_Finance_TransactionCategories_La~",
                table: "Finance_CollectionPolicies");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeRefundRequests_Finance_BankAccounts_PayingBankAc~",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeRefundRequests_Finance_TransactionCategories_Exp~",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_PaymentMethodRecords_Finance_BankAccounts_BankAccou~",
                table: "Finance_PaymentMethodRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_RefundPolicies_Finance_BankAccounts_DefaultPayingBa~",
                table: "Finance_RefundPolicies");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_RefundPolicies_Finance_TransactionCategories_Expens~",
                table: "Finance_RefundPolicies");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_Transactions_Finance_BankAccounts_BankAccountId",
                table: "Finance_Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Finance_Transactions_BankAccountId",
                table: "Finance_Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Finance_Transactions_SourceKey",
                table: "Finance_Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Finance_RefundPolicies_DefaultPayingBankAccountId",
                table: "Finance_RefundPolicies");

            migrationBuilder.DropIndex(
                name: "IX_Finance_RefundPolicies_ExpenseCategoryId",
                table: "Finance_RefundPolicies");

            migrationBuilder.DropIndex(
                name: "IX_Finance_PaymentMethodRecords_BankAccountId",
                table: "Finance_PaymentMethodRecords");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeRefundRequests_ExpenseCategoryId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeRefundRequests_PayingBankAccountId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropIndex(
                name: "IX_Finance_CollectionPolicies_LateFeeIncomeCategoryId",
                table: "Finance_CollectionPolicies");

            migrationBuilder.DropIndex(
                name: "IX_Finance_Charges_ReceivingBankAccountId",
                table: "Finance_Charges");

            migrationBuilder.DropIndex(
                name: "IX_Finance_ChargeRequests_IncomeCategoryId",
                table: "Finance_ChargeRequests");

            migrationBuilder.DropIndex(
                name: "IX_Finance_ChargeRequests_ReceivingBankAccountId",
                table: "Finance_ChargeRequests");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "Finance_Transactions");

            migrationBuilder.DropColumn(
                name: "BankLabelSnapshot",
                table: "Finance_Transactions");

            migrationBuilder.DropColumn(
                name: "CategoryNameSnapshot",
                table: "Finance_Transactions");

            migrationBuilder.DropColumn(
                name: "IsConfirmed",
                table: "Finance_Transactions");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "Finance_Transactions");

            migrationBuilder.DropColumn(
                name: "SourceKey",
                table: "Finance_Transactions");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "Finance_Transactions");

            migrationBuilder.DropColumn(
                name: "DefaultPayingBankAccountId",
                table: "Finance_RefundPolicies");

            migrationBuilder.DropColumn(
                name: "ExpenseCategoryId",
                table: "Finance_RefundPolicies");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "Finance_PaymentMethodRecords");

            migrationBuilder.DropColumn(
                name: "BankLabelSnapshot",
                table: "Finance_PaymentMethodRecords");

            migrationBuilder.DropColumn(
                name: "ExpenseCategoryId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "ExpenseCategoryName",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "PayingBankAccountId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "PayingBankLabel",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "LateFeeIncomeCategoryId",
                table: "Finance_CollectionPolicies");

            migrationBuilder.DropColumn(
                name: "IncomeCategoryName",
                table: "Finance_Charges");

            migrationBuilder.DropColumn(
                name: "ReceivingBankAccountId",
                table: "Finance_Charges");

            migrationBuilder.DropColumn(
                name: "ReceivingBankLabel",
                table: "Finance_Charges");

            migrationBuilder.DropColumn(
                name: "IncomeCategoryId",
                table: "Finance_ChargeRequests");

            migrationBuilder.DropColumn(
                name: "IncomeCategoryName",
                table: "Finance_ChargeRequests");

            migrationBuilder.DropColumn(
                name: "ReceivingBankAccountId",
                table: "Finance_ChargeRequests");

            migrationBuilder.DropColumn(
                name: "ReceivingBankLabel",
                table: "Finance_ChargeRequests");
        }
    }
}
