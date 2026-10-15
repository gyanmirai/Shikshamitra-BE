using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionFeeAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FinanceFeeAccountId",
                table: "Finance_Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceFeeAccountId",
                table: "Finance_StudentFeeStructures",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "Finance_PaymentMethodRecords",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceFeeAccountId",
                table: "Finance_PaymentMethodRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "Finance_FeeRefundRequests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceFeeAccountId",
                table: "Finance_FeeRefundRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "Finance_FeeRecordMetadatas",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceFeeAccountId",
                table: "Finance_FeeRecordMetadatas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "Finance_FeePaymentReceipts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceFeeAccountId",
                table: "Finance_FeePaymentReceipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "Finance_FeeCollectionRecords",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceFeeAccountId",
                table: "Finance_FeeCollectionRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "Finance_DueFees",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceFeeAccountId",
                table: "Finance_DueFees",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Finance_FeeAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediumStageId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicantName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentLinkedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StructureId = table.Column<Guid>(type: "uuid", nullable: true),
                    StructureRevision = table.Column<int>(type: "integer", nullable: true),
                    AssignmentSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    AssignmentRequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StudentFeeStructureId = table.Column<Guid>(type: "uuid", nullable: true),
                    HasPaymentEvidence = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Finance_FeeAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_FeeAccounts_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Finance_FeeAccounts_Finance_StudentFeeStructures_StudentFee~",
                        column: x => x.StudentFeeStructureId,
                        principalTable: "Finance_StudentFeeStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_Transactions_FinanceFeeAccountId",
                table: "Finance_Transactions",
                column: "FinanceFeeAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_StudentFeeStructures_FinanceFeeAccountId",
                table: "Finance_StudentFeeStructures",
                column: "FinanceFeeAccountId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"FinanceFeeAccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_PaymentMethodRecords_FinanceFeeAccountId",
                table: "Finance_PaymentMethodRecords",
                column: "FinanceFeeAccountId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentMethodRecords_Payer",
                table: "Finance_PaymentMethodRecords",
                sql: "\"StudentId\" IS NOT NULL OR \"FinanceFeeAccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeRefundRequests_FinanceFeeAccountId",
                table: "Finance_FeeRefundRequests",
                column: "FinanceFeeAccountId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FeeRefundRequests_Payer",
                table: "Finance_FeeRefundRequests",
                sql: "\"StudentId\" IS NOT NULL OR \"FinanceFeeAccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeRecordMetadatas_FinanceFeeAccountId",
                table: "Finance_FeeRecordMetadatas",
                column: "FinanceFeeAccountId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FeeRecordMetadatas_Payer",
                table: "Finance_FeeRecordMetadatas",
                sql: "\"StudentId\" IS NOT NULL OR \"FinanceFeeAccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeePaymentReceipts_FinanceFeeAccountId",
                table: "Finance_FeePaymentReceipts",
                column: "FinanceFeeAccountId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FeePaymentReceipts_Payer",
                table: "Finance_FeePaymentReceipts",
                sql: "\"StudentId\" IS NOT NULL OR \"FinanceFeeAccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeCollectionRecords_FinanceFeeAccountId",
                table: "Finance_FeeCollectionRecords",
                column: "FinanceFeeAccountId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FeeCollectionRecords_Payer",
                table: "Finance_FeeCollectionRecords",
                sql: "\"StudentId\" IS NOT NULL OR \"FinanceFeeAccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_DueFees_FinanceFeeAccountId",
                table: "Finance_DueFees",
                column: "FinanceFeeAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_DueFees_Tenant_Account_Schedule",
                table: "Finance_DueFees",
                columns: new[] { "TenantId", "FinanceFeeAccountId", "ScheduleInstanceKey" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"FinanceFeeAccountId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DueFees_Payer",
                table: "Finance_DueFees",
                sql: "\"StudentId\" IS NOT NULL OR \"FinanceFeeAccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FeeAccounts_AcademicScope",
                table: "Finance_FeeAccounts",
                columns: new[] { "TenantId", "AcademicYearId", "BoardId", "EducationMediumId", "StandardId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeAccounts_Tenant_Student_Year",
                table: "Finance_FeeAccounts",
                columns: new[] { "TenantId", "StudentId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeAccounts_StudentFeeStructureId",
                table: "Finance_FeeAccounts",
                column: "StudentFeeStructureId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeAccounts_StudentId",
                table: "Finance_FeeAccounts",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "UX_FeeAccounts_Tenant_Application_Year",
                table: "Finance_FeeAccounts",
                columns: new[] { "TenantId", "ApplicationId", "AcademicYearId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_DueFees_Finance_FeeAccounts_FinanceFeeAccountId",
                table: "Finance_DueFees",
                column: "FinanceFeeAccountId",
                principalTable: "Finance_FeeAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeCollectionRecords_Finance_FeeAccounts_FinanceFee~",
                table: "Finance_FeeCollectionRecords",
                column: "FinanceFeeAccountId",
                principalTable: "Finance_FeeAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeePaymentReceipts_Finance_FeeAccounts_FinanceFeeAc~",
                table: "Finance_FeePaymentReceipts",
                column: "FinanceFeeAccountId",
                principalTable: "Finance_FeeAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeRecordMetadatas_Finance_FeeAccounts_FinanceFeeAc~",
                table: "Finance_FeeRecordMetadatas",
                column: "FinanceFeeAccountId",
                principalTable: "Finance_FeeAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeRefundRequests_Finance_FeeAccounts_FinanceFeeAcc~",
                table: "Finance_FeeRefundRequests",
                column: "FinanceFeeAccountId",
                principalTable: "Finance_FeeAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_PaymentMethodRecords_Finance_FeeAccounts_FinanceFee~",
                table: "Finance_PaymentMethodRecords",
                column: "FinanceFeeAccountId",
                principalTable: "Finance_FeeAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_StudentFeeStructures_Finance_FeeAccounts_FinanceFee~",
                table: "Finance_StudentFeeStructures",
                column: "FinanceFeeAccountId",
                principalTable: "Finance_FeeAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_Transactions_Finance_FeeAccounts_FinanceFeeAccountId",
                table: "Finance_Transactions",
                column: "FinanceFeeAccountId",
                principalTable: "Finance_FeeAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Preserve the original evidence rather than restoring missing Student IDs.
            migrationBuilder.Sql("""
                DO $finance_fee_accounts_rollback$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Finance_FeeAccounts") THEN
                        RAISE EXCEPTION 'Admission Finance accounts exist. Preserve the original financial evidence and use a forward fix.';
                    END IF;
                END $finance_fee_accounts_rollback$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Finance_DueFees_Finance_FeeAccounts_FinanceFeeAccountId",
                table: "Finance_DueFees");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeCollectionRecords_Finance_FeeAccounts_FinanceFee~",
                table: "Finance_FeeCollectionRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeePaymentReceipts_Finance_FeeAccounts_FinanceFeeAc~",
                table: "Finance_FeePaymentReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeRecordMetadatas_Finance_FeeAccounts_FinanceFeeAc~",
                table: "Finance_FeeRecordMetadatas");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeRefundRequests_Finance_FeeAccounts_FinanceFeeAcc~",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_PaymentMethodRecords_Finance_FeeAccounts_FinanceFee~",
                table: "Finance_PaymentMethodRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_StudentFeeStructures_Finance_FeeAccounts_FinanceFee~",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_Transactions_Finance_FeeAccounts_FinanceFeeAccountId",
                table: "Finance_Transactions");

            migrationBuilder.DropTable(
                name: "Finance_FeeAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Finance_Transactions_FinanceFeeAccountId",
                table: "Finance_Transactions");

            migrationBuilder.DropIndex(
                name: "UX_StudentFeeStructures_FinanceFeeAccountId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_Finance_PaymentMethodRecords_FinanceFeeAccountId",
                table: "Finance_PaymentMethodRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentMethodRecords_Payer",
                table: "Finance_PaymentMethodRecords");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeRefundRequests_FinanceFeeAccountId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FeeRefundRequests_Payer",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeRecordMetadatas_FinanceFeeAccountId",
                table: "Finance_FeeRecordMetadatas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FeeRecordMetadatas_Payer",
                table: "Finance_FeeRecordMetadatas");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeePaymentReceipts_FinanceFeeAccountId",
                table: "Finance_FeePaymentReceipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FeePaymentReceipts_Payer",
                table: "Finance_FeePaymentReceipts");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeCollectionRecords_FinanceFeeAccountId",
                table: "Finance_FeeCollectionRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FeeCollectionRecords_Payer",
                table: "Finance_FeeCollectionRecords");

            migrationBuilder.DropIndex(
                name: "IX_Finance_DueFees_FinanceFeeAccountId",
                table: "Finance_DueFees");

            migrationBuilder.DropIndex(
                name: "UX_DueFees_Tenant_Account_Schedule",
                table: "Finance_DueFees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DueFees_Payer",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "FinanceFeeAccountId",
                table: "Finance_Transactions");

            migrationBuilder.DropColumn(
                name: "FinanceFeeAccountId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "FinanceFeeAccountId",
                table: "Finance_PaymentMethodRecords");

            migrationBuilder.DropColumn(
                name: "FinanceFeeAccountId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "FinanceFeeAccountId",
                table: "Finance_FeeRecordMetadatas");

            migrationBuilder.DropColumn(
                name: "FinanceFeeAccountId",
                table: "Finance_FeePaymentReceipts");

            migrationBuilder.DropColumn(
                name: "FinanceFeeAccountId",
                table: "Finance_FeeCollectionRecords");

            migrationBuilder.DropColumn(
                name: "FinanceFeeAccountId",
                table: "Finance_DueFees");

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "Finance_PaymentMethodRecords",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "Finance_FeeRefundRequests",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "Finance_FeeRecordMetadatas",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "Finance_FeePaymentReceipts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "Finance_FeeCollectionRecords",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "Finance_DueFees",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

        }
    }
}
