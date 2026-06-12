using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppConcessionDueFeeAllocations_AppConcessionRequests_Conces~",
                table: "AppConcessionDueFeeAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_AppConcessionFeeHeadMappings_AppConcessionRequests_Concessi~",
                table: "AppConcessionFeeHeadMappings");

            migrationBuilder.DropForeignKey(
                name: "FK_AppConcessionRequestDocuments_AppConcessionRequests_Concess~",
                table: "AppConcessionRequestDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_AppConcessionTypeDocumentFormats_ConcessionTypeDocuments_Co~",
                table: "AppConcessionTypeDocumentFormats");

            migrationBuilder.DropForeignKey(
                name: "FK_AppIncomeCategoryTags_TransactionCategories_TransactionCate~",
                table: "AppIncomeCategoryTags");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_FeeStructures_FeeStructureId",
                table: "AppStudents");

            migrationBuilder.DropForeignKey(
                name: "FK_AppTransactionCategoryTags_Transactions_TransactionId",
                table: "AppTransactionCategoryTags");

            migrationBuilder.DropForeignKey(
                name: "FK_ConcessionTypeDocuments_ConcessionTypes_ConcessionTypeId",
                table: "ConcessionTypeDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_FeeHeads_FeeCategories_FeeCategoryId",
                table: "FeeHeads");

            migrationBuilder.DropForeignKey(
                name: "FK_FeeRefundLines_FeeRefundRequests_FeeRefundRequestId",
                table: "FeeRefundLines");

            migrationBuilder.DropForeignKey(
                name: "FK_FeeStructureDetails_FeeHeads_FeeHeadId",
                table: "FeeStructureDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_FeeStructureDetails_FeeStructures_FeeStructureId",
                table: "FeeStructureDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_FeeStructureMetadatas_AppAcademicYears_AcademicYearId",
                table: "FeeStructureMetadatas");

            migrationBuilder.DropForeignKey(
                name: "FK_FeeStructureMetadatas_AppEducationBoards_AcademicBoardId",
                table: "FeeStructureMetadatas");

            migrationBuilder.DropForeignKey(
                name: "FK_FeeStructureMetadatas_AppEducationMediums_AcademicMediumId",
                table: "FeeStructureMetadatas");

            migrationBuilder.DropForeignKey(
                name: "FK_FeeStructureMetadatas_ReceiptBooks_ReceiptBookId",
                table: "FeeStructureMetadatas");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptBooks_AppAcademicYears_AcademicYearId",
                table: "ReceiptBooks");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptBooks_AppEducationBoards_BoardId",
                table: "ReceiptBooks");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptBooks_AppEducationMediumStages_MediumStageId",
                table: "ReceiptBooks");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptBooks_AppEducationMediums_MediumId",
                table: "ReceiptBooks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Transactions",
                table: "Transactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TransactionCategories",
                table: "TransactionCategories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StudentFeeStructures",
                table: "StudentFeeStructures");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StudentFeeRecords",
                table: "StudentFeeRecords");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ReceiptBooks",
                table: "ReceiptBooks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PaymentMethodRecords",
                table: "PaymentMethodRecords");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FinanceReceiptNumberSequences",
                table: "FinanceReceiptNumberSequences");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FeeStructures",
                table: "FeeStructures");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FeeStructureMetadatas",
                table: "FeeStructureMetadatas");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FeeStructureDetails",
                table: "FeeStructureDetails");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FeeRefundRequests",
                table: "FeeRefundRequests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FeeRecordMetadatas",
                table: "FeeRecordMetadatas");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FeePaymentReceipts",
                table: "FeePaymentReceipts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FeeHeads",
                table: "FeeHeads");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FeeCollectionRecords",
                table: "FeeCollectionRecords");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FeeCollectionDueFeeAllocations",
                table: "FeeCollectionDueFeeAllocations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FeeCategories",
                table: "FeeCategories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DueFees",
                table: "DueFees");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ConcessionTypes",
                table: "ConcessionTypes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ConcessionTypeDocuments",
                table: "ConcessionTypeDocuments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Charges",
                table: "Charges");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChargeRequests",
                table: "ChargeRequests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChargeCollectionRecords",
                table: "ChargeCollectionRecords");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChargeCategories",
                table: "ChargeCategories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BankAccounts",
                table: "BankAccounts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppConcessionTypeDocumentFormats",
                table: "AppConcessionTypeDocumentFormats");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppConcessionRequests",
                table: "AppConcessionRequests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppConcessionRequestDocuments",
                table: "AppConcessionRequestDocuments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppConcessionFeeHeadMappings",
                table: "AppConcessionFeeHeadMappings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppConcessionDueFeeAllocations",
                table: "AppConcessionDueFeeAllocations");

            migrationBuilder.RenameTable(
                name: "Transactions",
                newName: "Finance_Transactions");

            migrationBuilder.RenameTable(
                name: "TransactionCategories",
                newName: "Finance_TransactionCategories");

            migrationBuilder.RenameTable(
                name: "StudentFeeStructures",
                newName: "Finance_StudentFeeStructures");

            migrationBuilder.RenameTable(
                name: "StudentFeeRecords",
                newName: "Finance_StudentFeeRecords");

            migrationBuilder.RenameTable(
                name: "ReceiptBooks",
                newName: "Finance_ReceiptBooks");

            migrationBuilder.RenameTable(
                name: "PaymentMethodRecords",
                newName: "Finance_PaymentMethodRecords");

            migrationBuilder.RenameTable(
                name: "FinanceReceiptNumberSequences",
                newName: "Finance_FinanceReceiptNumberSequences");

            migrationBuilder.RenameTable(
                name: "FeeStructures",
                newName: "Finance_FeeStructures");

            migrationBuilder.RenameTable(
                name: "FeeStructureMetadatas",
                newName: "Finance_FeeStructureMetadatas");

            migrationBuilder.RenameTable(
                name: "FeeStructureDetails",
                newName: "Finance_FeeStructureDetails");

            migrationBuilder.RenameTable(
                name: "FeeRefundRequests",
                newName: "Finance_FeeRefundRequests");

            migrationBuilder.RenameTable(
                name: "FeeRecordMetadatas",
                newName: "Finance_FeeRecordMetadatas");

            migrationBuilder.RenameTable(
                name: "FeePaymentReceipts",
                newName: "Finance_FeePaymentReceipts");

            migrationBuilder.RenameTable(
                name: "FeeHeads",
                newName: "Finance_FeeHeads");

            migrationBuilder.RenameTable(
                name: "FeeCollectionRecords",
                newName: "Finance_FeeCollectionRecords");

            migrationBuilder.RenameTable(
                name: "FeeCollectionDueFeeAllocations",
                newName: "Finance_FeeCollectionDueFeeAllocations");

            migrationBuilder.RenameTable(
                name: "FeeCategories",
                newName: "Finance_FeeCategories");

            migrationBuilder.RenameTable(
                name: "DueFees",
                newName: "Finance_DueFees");

            migrationBuilder.RenameTable(
                name: "ConcessionTypes",
                newName: "Finance_ConcessionTypes");

            migrationBuilder.RenameTable(
                name: "ConcessionTypeDocuments",
                newName: "Finance_ConcessionTypeDocuments");

            migrationBuilder.RenameTable(
                name: "Charges",
                newName: "Finance_Charges");

            migrationBuilder.RenameTable(
                name: "ChargeRequests",
                newName: "Finance_ChargeRequests");

            migrationBuilder.RenameTable(
                name: "ChargeCollectionRecords",
                newName: "Finance_ChargeCollectionRecords");

            migrationBuilder.RenameTable(
                name: "ChargeCategories",
                newName: "Finance_ChargeCategories");

            migrationBuilder.RenameTable(
                name: "BankAccounts",
                newName: "Finance_BankAccounts");

            migrationBuilder.RenameTable(
                name: "AppConcessionTypeDocumentFormats",
                newName: "Finance_ConcessionTypeDocumentFormats");

            migrationBuilder.RenameTable(
                name: "AppConcessionRequests",
                newName: "Finance_ConcessionRequests");

            migrationBuilder.RenameTable(
                name: "AppConcessionRequestDocuments",
                newName: "Finance_ConcessionRequestDocuments");

            migrationBuilder.RenameTable(
                name: "AppConcessionFeeHeadMappings",
                newName: "Finance_ConcessionFeeHeadMappings");

            migrationBuilder.RenameTable(
                name: "AppConcessionDueFeeAllocations",
                newName: "Finance_ConcessionDueFeeAllocations");

            migrationBuilder.RenameIndex(
                name: "IX_StudentFeeStructures_StudentId",
                table: "Finance_StudentFeeStructures",
                newName: "IX_Finance_StudentFeeStructures_StudentId");

            migrationBuilder.RenameIndex(
                name: "IX_ReceiptBooks_MediumStageId",
                table: "Finance_ReceiptBooks",
                newName: "IX_Finance_ReceiptBooks_MediumStageId");

            migrationBuilder.RenameIndex(
                name: "IX_ReceiptBooks_MediumId",
                table: "Finance_ReceiptBooks",
                newName: "IX_Finance_ReceiptBooks_MediumId");

            migrationBuilder.RenameIndex(
                name: "IX_ReceiptBooks_BoardId",
                table: "Finance_ReceiptBooks",
                newName: "IX_Finance_ReceiptBooks_BoardId");

            migrationBuilder.RenameIndex(
                name: "IX_ReceiptBooks_AcademicYearId",
                table: "Finance_ReceiptBooks",
                newName: "IX_Finance_ReceiptBooks_AcademicYearId");

            migrationBuilder.RenameIndex(
                name: "IX_FeeStructureMetadatas_ReceiptBookId",
                table: "Finance_FeeStructureMetadatas",
                newName: "IX_Finance_FeeStructureMetadatas_ReceiptBookId");

            migrationBuilder.RenameIndex(
                name: "IX_FeeStructureMetadatas_AcademicYearId",
                table: "Finance_FeeStructureMetadatas",
                newName: "IX_Finance_FeeStructureMetadatas_AcademicYearId");

            migrationBuilder.RenameIndex(
                name: "IX_FeeStructureMetadatas_AcademicMediumId",
                table: "Finance_FeeStructureMetadatas",
                newName: "IX_Finance_FeeStructureMetadatas_AcademicMediumId");

            migrationBuilder.RenameIndex(
                name: "IX_FeeStructureMetadatas_AcademicBoardId",
                table: "Finance_FeeStructureMetadatas",
                newName: "IX_Finance_FeeStructureMetadatas_AcademicBoardId");

            migrationBuilder.RenameIndex(
                name: "IX_FeeStructureDetails_FeeStructureId",
                table: "Finance_FeeStructureDetails",
                newName: "IX_Finance_FeeStructureDetails_FeeStructureId");

            migrationBuilder.RenameIndex(
                name: "IX_FeeStructureDetails_FeeHeadId",
                table: "Finance_FeeStructureDetails",
                newName: "IX_Finance_FeeStructureDetails_FeeHeadId");

            migrationBuilder.RenameIndex(
                name: "IX_FeeHeads_FeeCategoryId",
                table: "Finance_FeeHeads",
                newName: "IX_Finance_FeeHeads_FeeCategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_ConcessionTypeDocuments_ConcessionTypeId",
                table: "Finance_ConcessionTypeDocuments",
                newName: "IX_Finance_ConcessionTypeDocuments_ConcessionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_AppConcessionTypeDocumentFormats_ConcessionTypeDocumentId",
                table: "Finance_ConcessionTypeDocumentFormats",
                newName: "IX_Finance_ConcessionTypeDocumentFormats_ConcessionTypeDocumen~");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_Transactions",
                table: "Finance_Transactions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_TransactionCategories",
                table: "Finance_TransactionCategories",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_StudentFeeStructures",
                table: "Finance_StudentFeeStructures",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_StudentFeeRecords",
                table: "Finance_StudentFeeRecords",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_ReceiptBooks",
                table: "Finance_ReceiptBooks",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_PaymentMethodRecords",
                table: "Finance_PaymentMethodRecords",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_FinanceReceiptNumberSequences",
                table: "Finance_FinanceReceiptNumberSequences",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_FeeStructures",
                table: "Finance_FeeStructures",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_FeeStructureMetadatas",
                table: "Finance_FeeStructureMetadatas",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_FeeStructureDetails",
                table: "Finance_FeeStructureDetails",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_FeeRefundRequests",
                table: "Finance_FeeRefundRequests",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_FeeRecordMetadatas",
                table: "Finance_FeeRecordMetadatas",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_FeePaymentReceipts",
                table: "Finance_FeePaymentReceipts",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_FeeHeads",
                table: "Finance_FeeHeads",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_FeeCollectionRecords",
                table: "Finance_FeeCollectionRecords",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_FeeCollectionDueFeeAllocations",
                table: "Finance_FeeCollectionDueFeeAllocations",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_FeeCategories",
                table: "Finance_FeeCategories",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_DueFees",
                table: "Finance_DueFees",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_ConcessionTypes",
                table: "Finance_ConcessionTypes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_ConcessionTypeDocuments",
                table: "Finance_ConcessionTypeDocuments",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_Charges",
                table: "Finance_Charges",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_ChargeRequests",
                table: "Finance_ChargeRequests",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_ChargeCollectionRecords",
                table: "Finance_ChargeCollectionRecords",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_ChargeCategories",
                table: "Finance_ChargeCategories",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_BankAccounts",
                table: "Finance_BankAccounts",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_ConcessionTypeDocumentFormats",
                table: "Finance_ConcessionTypeDocumentFormats",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_ConcessionRequests",
                table: "Finance_ConcessionRequests",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_ConcessionRequestDocuments",
                table: "Finance_ConcessionRequestDocuments",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_ConcessionFeeHeadMappings",
                table: "Finance_ConcessionFeeHeadMappings",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Finance_ConcessionDueFeeAllocations",
                table: "Finance_ConcessionDueFeeAllocations",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppIncomeCategoryTags_Finance_TransactionCategories_Transac~",
                table: "AppIncomeCategoryTags",
                column: "TransactionCategoryId",
                principalTable: "Finance_TransactionCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_Finance_FeeStructures_FeeStructureId",
                table: "AppStudents",
                column: "FeeStructureId",
                principalTable: "Finance_FeeStructures",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppTransactionCategoryTags_Finance_Transactions_Transaction~",
                table: "AppTransactionCategoryTags",
                column: "TransactionId",
                principalTable: "Finance_Transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FeeRefundLines_Finance_FeeRefundRequests_FeeRefundRequestId",
                table: "FeeRefundLines",
                column: "FeeRefundRequestId",
                principalTable: "Finance_FeeRefundRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_ConcessionDueFeeAllocations_Finance_ConcessionReque~",
                table: "Finance_ConcessionDueFeeAllocations",
                column: "ConcessionRequestId",
                principalTable: "Finance_ConcessionRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_ConcessionFeeHeadMappings_Finance_ConcessionRequest~",
                table: "Finance_ConcessionFeeHeadMappings",
                column: "ConcessionRequestId",
                principalTable: "Finance_ConcessionRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_ConcessionRequestDocuments_Finance_ConcessionReques~",
                table: "Finance_ConcessionRequestDocuments",
                column: "ConcessionRequestId",
                principalTable: "Finance_ConcessionRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_ConcessionTypeDocumentFormats_Finance_ConcessionTyp~",
                table: "Finance_ConcessionTypeDocumentFormats",
                column: "ConcessionTypeDocumentId",
                principalTable: "Finance_ConcessionTypeDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_ConcessionTypeDocuments_Finance_ConcessionTypes_Con~",
                table: "Finance_ConcessionTypeDocuments",
                column: "ConcessionTypeId",
                principalTable: "Finance_ConcessionTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeHeads_Finance_FeeCategories_FeeCategoryId",
                table: "Finance_FeeHeads",
                column: "FeeCategoryId",
                principalTable: "Finance_FeeCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeStructureDetails_Finance_FeeHeads_FeeHeadId",
                table: "Finance_FeeStructureDetails",
                column: "FeeHeadId",
                principalTable: "Finance_FeeHeads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeStructureDetails_Finance_FeeStructures_FeeStruct~",
                table: "Finance_FeeStructureDetails",
                column: "FeeStructureId",
                principalTable: "Finance_FeeStructures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeStructureMetadatas_AppAcademicYears_AcademicYear~",
                table: "Finance_FeeStructureMetadatas",
                column: "AcademicYearId",
                principalTable: "AppAcademicYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeStructureMetadatas_AppEducationBoards_AcademicBo~",
                table: "Finance_FeeStructureMetadatas",
                column: "AcademicBoardId",
                principalTable: "AppEducationBoards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeStructureMetadatas_AppEducationMediums_AcademicM~",
                table: "Finance_FeeStructureMetadatas",
                column: "AcademicMediumId",
                principalTable: "AppEducationMediums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeStructureMetadatas_Finance_ReceiptBooks_ReceiptB~",
                table: "Finance_FeeStructureMetadatas",
                column: "ReceiptBookId",
                principalTable: "Finance_ReceiptBooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_ReceiptBooks_AppAcademicYears_AcademicYearId",
                table: "Finance_ReceiptBooks",
                column: "AcademicYearId",
                principalTable: "AppAcademicYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_ReceiptBooks_AppEducationBoards_BoardId",
                table: "Finance_ReceiptBooks",
                column: "BoardId",
                principalTable: "AppEducationBoards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_ReceiptBooks_AppEducationMediumStages_MediumStageId",
                table: "Finance_ReceiptBooks",
                column: "MediumStageId",
                principalTable: "AppEducationMediumStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_ReceiptBooks_AppEducationMediums_MediumId",
                table: "Finance_ReceiptBooks",
                column: "MediumId",
                principalTable: "AppEducationMediums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppIncomeCategoryTags_Finance_TransactionCategories_Transac~",
                table: "AppIncomeCategoryTags");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_Finance_FeeStructures_FeeStructureId",
                table: "AppStudents");

            migrationBuilder.DropForeignKey(
                name: "FK_AppTransactionCategoryTags_Finance_Transactions_Transaction~",
                table: "AppTransactionCategoryTags");

            migrationBuilder.DropForeignKey(
                name: "FK_FeeRefundLines_Finance_FeeRefundRequests_FeeRefundRequestId",
                table: "FeeRefundLines");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_ConcessionDueFeeAllocations_Finance_ConcessionReque~",
                table: "Finance_ConcessionDueFeeAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_ConcessionFeeHeadMappings_Finance_ConcessionRequest~",
                table: "Finance_ConcessionFeeHeadMappings");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_ConcessionRequestDocuments_Finance_ConcessionReques~",
                table: "Finance_ConcessionRequestDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_ConcessionTypeDocumentFormats_Finance_ConcessionTyp~",
                table: "Finance_ConcessionTypeDocumentFormats");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_ConcessionTypeDocuments_Finance_ConcessionTypes_Con~",
                table: "Finance_ConcessionTypeDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeHeads_Finance_FeeCategories_FeeCategoryId",
                table: "Finance_FeeHeads");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeStructureDetails_Finance_FeeHeads_FeeHeadId",
                table: "Finance_FeeStructureDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeStructureDetails_Finance_FeeStructures_FeeStruct~",
                table: "Finance_FeeStructureDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeStructureMetadatas_AppAcademicYears_AcademicYear~",
                table: "Finance_FeeStructureMetadatas");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeStructureMetadatas_AppEducationBoards_AcademicBo~",
                table: "Finance_FeeStructureMetadatas");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeStructureMetadatas_AppEducationMediums_AcademicM~",
                table: "Finance_FeeStructureMetadatas");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeStructureMetadatas_Finance_ReceiptBooks_ReceiptB~",
                table: "Finance_FeeStructureMetadatas");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_ReceiptBooks_AppAcademicYears_AcademicYearId",
                table: "Finance_ReceiptBooks");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_ReceiptBooks_AppEducationBoards_BoardId",
                table: "Finance_ReceiptBooks");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_ReceiptBooks_AppEducationMediumStages_MediumStageId",
                table: "Finance_ReceiptBooks");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_ReceiptBooks_AppEducationMediums_MediumId",
                table: "Finance_ReceiptBooks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_Transactions",
                table: "Finance_Transactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_TransactionCategories",
                table: "Finance_TransactionCategories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_StudentFeeStructures",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_StudentFeeRecords",
                table: "Finance_StudentFeeRecords");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_ReceiptBooks",
                table: "Finance_ReceiptBooks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_PaymentMethodRecords",
                table: "Finance_PaymentMethodRecords");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_FinanceReceiptNumberSequences",
                table: "Finance_FinanceReceiptNumberSequences");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_FeeStructures",
                table: "Finance_FeeStructures");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_FeeStructureMetadatas",
                table: "Finance_FeeStructureMetadatas");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_FeeStructureDetails",
                table: "Finance_FeeStructureDetails");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_FeeRefundRequests",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_FeeRecordMetadatas",
                table: "Finance_FeeRecordMetadatas");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_FeePaymentReceipts",
                table: "Finance_FeePaymentReceipts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_FeeHeads",
                table: "Finance_FeeHeads");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_FeeCollectionRecords",
                table: "Finance_FeeCollectionRecords");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_FeeCollectionDueFeeAllocations",
                table: "Finance_FeeCollectionDueFeeAllocations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_FeeCategories",
                table: "Finance_FeeCategories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_DueFees",
                table: "Finance_DueFees");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_ConcessionTypes",
                table: "Finance_ConcessionTypes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_ConcessionTypeDocuments",
                table: "Finance_ConcessionTypeDocuments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_ConcessionTypeDocumentFormats",
                table: "Finance_ConcessionTypeDocumentFormats");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_ConcessionRequests",
                table: "Finance_ConcessionRequests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_ConcessionRequestDocuments",
                table: "Finance_ConcessionRequestDocuments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_ConcessionFeeHeadMappings",
                table: "Finance_ConcessionFeeHeadMappings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_ConcessionDueFeeAllocations",
                table: "Finance_ConcessionDueFeeAllocations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_Charges",
                table: "Finance_Charges");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_ChargeRequests",
                table: "Finance_ChargeRequests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_ChargeCollectionRecords",
                table: "Finance_ChargeCollectionRecords");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_ChargeCategories",
                table: "Finance_ChargeCategories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Finance_BankAccounts",
                table: "Finance_BankAccounts");

            migrationBuilder.RenameTable(
                name: "Finance_Transactions",
                newName: "Transactions");

            migrationBuilder.RenameTable(
                name: "Finance_TransactionCategories",
                newName: "TransactionCategories");

            migrationBuilder.RenameTable(
                name: "Finance_StudentFeeStructures",
                newName: "StudentFeeStructures");

            migrationBuilder.RenameTable(
                name: "Finance_StudentFeeRecords",
                newName: "StudentFeeRecords");

            migrationBuilder.RenameTable(
                name: "Finance_ReceiptBooks",
                newName: "ReceiptBooks");

            migrationBuilder.RenameTable(
                name: "Finance_PaymentMethodRecords",
                newName: "PaymentMethodRecords");

            migrationBuilder.RenameTable(
                name: "Finance_FinanceReceiptNumberSequences",
                newName: "FinanceReceiptNumberSequences");

            migrationBuilder.RenameTable(
                name: "Finance_FeeStructures",
                newName: "FeeStructures");

            migrationBuilder.RenameTable(
                name: "Finance_FeeStructureMetadatas",
                newName: "FeeStructureMetadatas");

            migrationBuilder.RenameTable(
                name: "Finance_FeeStructureDetails",
                newName: "FeeStructureDetails");

            migrationBuilder.RenameTable(
                name: "Finance_FeeRefundRequests",
                newName: "FeeRefundRequests");

            migrationBuilder.RenameTable(
                name: "Finance_FeeRecordMetadatas",
                newName: "FeeRecordMetadatas");

            migrationBuilder.RenameTable(
                name: "Finance_FeePaymentReceipts",
                newName: "FeePaymentReceipts");

            migrationBuilder.RenameTable(
                name: "Finance_FeeHeads",
                newName: "FeeHeads");

            migrationBuilder.RenameTable(
                name: "Finance_FeeCollectionRecords",
                newName: "FeeCollectionRecords");

            migrationBuilder.RenameTable(
                name: "Finance_FeeCollectionDueFeeAllocations",
                newName: "FeeCollectionDueFeeAllocations");

            migrationBuilder.RenameTable(
                name: "Finance_FeeCategories",
                newName: "FeeCategories");

            migrationBuilder.RenameTable(
                name: "Finance_DueFees",
                newName: "DueFees");

            migrationBuilder.RenameTable(
                name: "Finance_ConcessionTypes",
                newName: "ConcessionTypes");

            migrationBuilder.RenameTable(
                name: "Finance_ConcessionTypeDocuments",
                newName: "ConcessionTypeDocuments");

            migrationBuilder.RenameTable(
                name: "Finance_ConcessionTypeDocumentFormats",
                newName: "AppConcessionTypeDocumentFormats");

            migrationBuilder.RenameTable(
                name: "Finance_ConcessionRequests",
                newName: "AppConcessionRequests");

            migrationBuilder.RenameTable(
                name: "Finance_ConcessionRequestDocuments",
                newName: "AppConcessionRequestDocuments");

            migrationBuilder.RenameTable(
                name: "Finance_ConcessionFeeHeadMappings",
                newName: "AppConcessionFeeHeadMappings");

            migrationBuilder.RenameTable(
                name: "Finance_ConcessionDueFeeAllocations",
                newName: "AppConcessionDueFeeAllocations");

            migrationBuilder.RenameTable(
                name: "Finance_Charges",
                newName: "Charges");

            migrationBuilder.RenameTable(
                name: "Finance_ChargeRequests",
                newName: "ChargeRequests");

            migrationBuilder.RenameTable(
                name: "Finance_ChargeCollectionRecords",
                newName: "ChargeCollectionRecords");

            migrationBuilder.RenameTable(
                name: "Finance_ChargeCategories",
                newName: "ChargeCategories");

            migrationBuilder.RenameTable(
                name: "Finance_BankAccounts",
                newName: "BankAccounts");

            migrationBuilder.RenameIndex(
                name: "IX_Finance_StudentFeeStructures_StudentId",
                table: "StudentFeeStructures",
                newName: "IX_StudentFeeStructures_StudentId");

            migrationBuilder.RenameIndex(
                name: "IX_Finance_ReceiptBooks_MediumStageId",
                table: "ReceiptBooks",
                newName: "IX_ReceiptBooks_MediumStageId");

            migrationBuilder.RenameIndex(
                name: "IX_Finance_ReceiptBooks_MediumId",
                table: "ReceiptBooks",
                newName: "IX_ReceiptBooks_MediumId");

            migrationBuilder.RenameIndex(
                name: "IX_Finance_ReceiptBooks_BoardId",
                table: "ReceiptBooks",
                newName: "IX_ReceiptBooks_BoardId");

            migrationBuilder.RenameIndex(
                name: "IX_Finance_ReceiptBooks_AcademicYearId",
                table: "ReceiptBooks",
                newName: "IX_ReceiptBooks_AcademicYearId");

            migrationBuilder.RenameIndex(
                name: "IX_Finance_FeeStructureMetadatas_ReceiptBookId",
                table: "FeeStructureMetadatas",
                newName: "IX_FeeStructureMetadatas_ReceiptBookId");

            migrationBuilder.RenameIndex(
                name: "IX_Finance_FeeStructureMetadatas_AcademicYearId",
                table: "FeeStructureMetadatas",
                newName: "IX_FeeStructureMetadatas_AcademicYearId");

            migrationBuilder.RenameIndex(
                name: "IX_Finance_FeeStructureMetadatas_AcademicMediumId",
                table: "FeeStructureMetadatas",
                newName: "IX_FeeStructureMetadatas_AcademicMediumId");

            migrationBuilder.RenameIndex(
                name: "IX_Finance_FeeStructureMetadatas_AcademicBoardId",
                table: "FeeStructureMetadatas",
                newName: "IX_FeeStructureMetadatas_AcademicBoardId");

            migrationBuilder.RenameIndex(
                name: "IX_Finance_FeeStructureDetails_FeeStructureId",
                table: "FeeStructureDetails",
                newName: "IX_FeeStructureDetails_FeeStructureId");

            migrationBuilder.RenameIndex(
                name: "IX_Finance_FeeStructureDetails_FeeHeadId",
                table: "FeeStructureDetails",
                newName: "IX_FeeStructureDetails_FeeHeadId");

            migrationBuilder.RenameIndex(
                name: "IX_Finance_FeeHeads_FeeCategoryId",
                table: "FeeHeads",
                newName: "IX_FeeHeads_FeeCategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Finance_ConcessionTypeDocuments_ConcessionTypeId",
                table: "ConcessionTypeDocuments",
                newName: "IX_ConcessionTypeDocuments_ConcessionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Finance_ConcessionTypeDocumentFormats_ConcessionTypeDocumen~",
                table: "AppConcessionTypeDocumentFormats",
                newName: "IX_AppConcessionTypeDocumentFormats_ConcessionTypeDocumentId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Transactions",
                table: "Transactions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TransactionCategories",
                table: "TransactionCategories",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StudentFeeStructures",
                table: "StudentFeeStructures",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StudentFeeRecords",
                table: "StudentFeeRecords",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ReceiptBooks",
                table: "ReceiptBooks",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PaymentMethodRecords",
                table: "PaymentMethodRecords",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FinanceReceiptNumberSequences",
                table: "FinanceReceiptNumberSequences",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FeeStructures",
                table: "FeeStructures",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FeeStructureMetadatas",
                table: "FeeStructureMetadatas",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FeeStructureDetails",
                table: "FeeStructureDetails",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FeeRefundRequests",
                table: "FeeRefundRequests",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FeeRecordMetadatas",
                table: "FeeRecordMetadatas",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FeePaymentReceipts",
                table: "FeePaymentReceipts",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FeeHeads",
                table: "FeeHeads",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FeeCollectionRecords",
                table: "FeeCollectionRecords",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FeeCollectionDueFeeAllocations",
                table: "FeeCollectionDueFeeAllocations",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FeeCategories",
                table: "FeeCategories",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DueFees",
                table: "DueFees",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ConcessionTypes",
                table: "ConcessionTypes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ConcessionTypeDocuments",
                table: "ConcessionTypeDocuments",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppConcessionTypeDocumentFormats",
                table: "AppConcessionTypeDocumentFormats",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppConcessionRequests",
                table: "AppConcessionRequests",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppConcessionRequestDocuments",
                table: "AppConcessionRequestDocuments",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppConcessionFeeHeadMappings",
                table: "AppConcessionFeeHeadMappings",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppConcessionDueFeeAllocations",
                table: "AppConcessionDueFeeAllocations",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Charges",
                table: "Charges",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChargeRequests",
                table: "ChargeRequests",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChargeCollectionRecords",
                table: "ChargeCollectionRecords",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChargeCategories",
                table: "ChargeCategories",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BankAccounts",
                table: "BankAccounts",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppConcessionDueFeeAllocations_AppConcessionRequests_Conces~",
                table: "AppConcessionDueFeeAllocations",
                column: "ConcessionRequestId",
                principalTable: "AppConcessionRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppConcessionFeeHeadMappings_AppConcessionRequests_Concessi~",
                table: "AppConcessionFeeHeadMappings",
                column: "ConcessionRequestId",
                principalTable: "AppConcessionRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppConcessionRequestDocuments_AppConcessionRequests_Concess~",
                table: "AppConcessionRequestDocuments",
                column: "ConcessionRequestId",
                principalTable: "AppConcessionRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppConcessionTypeDocumentFormats_ConcessionTypeDocuments_Co~",
                table: "AppConcessionTypeDocumentFormats",
                column: "ConcessionTypeDocumentId",
                principalTable: "ConcessionTypeDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppIncomeCategoryTags_TransactionCategories_TransactionCate~",
                table: "AppIncomeCategoryTags",
                column: "TransactionCategoryId",
                principalTable: "TransactionCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_FeeStructures_FeeStructureId",
                table: "AppStudents",
                column: "FeeStructureId",
                principalTable: "FeeStructures",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppTransactionCategoryTags_Transactions_TransactionId",
                table: "AppTransactionCategoryTags",
                column: "TransactionId",
                principalTable: "Transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConcessionTypeDocuments_ConcessionTypes_ConcessionTypeId",
                table: "ConcessionTypeDocuments",
                column: "ConcessionTypeId",
                principalTable: "ConcessionTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FeeHeads_FeeCategories_FeeCategoryId",
                table: "FeeHeads",
                column: "FeeCategoryId",
                principalTable: "FeeCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FeeRefundLines_FeeRefundRequests_FeeRefundRequestId",
                table: "FeeRefundLines",
                column: "FeeRefundRequestId",
                principalTable: "FeeRefundRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FeeStructureDetails_FeeHeads_FeeHeadId",
                table: "FeeStructureDetails",
                column: "FeeHeadId",
                principalTable: "FeeHeads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FeeStructureDetails_FeeStructures_FeeStructureId",
                table: "FeeStructureDetails",
                column: "FeeStructureId",
                principalTable: "FeeStructures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FeeStructureMetadatas_AppAcademicYears_AcademicYearId",
                table: "FeeStructureMetadatas",
                column: "AcademicYearId",
                principalTable: "AppAcademicYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FeeStructureMetadatas_AppEducationBoards_AcademicBoardId",
                table: "FeeStructureMetadatas",
                column: "AcademicBoardId",
                principalTable: "AppEducationBoards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FeeStructureMetadatas_AppEducationMediums_AcademicMediumId",
                table: "FeeStructureMetadatas",
                column: "AcademicMediumId",
                principalTable: "AppEducationMediums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FeeStructureMetadatas_ReceiptBooks_ReceiptBookId",
                table: "FeeStructureMetadatas",
                column: "ReceiptBookId",
                principalTable: "ReceiptBooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptBooks_AppAcademicYears_AcademicYearId",
                table: "ReceiptBooks",
                column: "AcademicYearId",
                principalTable: "AppAcademicYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptBooks_AppEducationBoards_BoardId",
                table: "ReceiptBooks",
                column: "BoardId",
                principalTable: "AppEducationBoards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptBooks_AppEducationMediumStages_MediumStageId",
                table: "ReceiptBooks",
                column: "MediumStageId",
                principalTable: "AppEducationMediumStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptBooks_AppEducationMediums_MediumId",
                table: "ReceiptBooks",
                column: "MediumId",
                principalTable: "AppEducationMediums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
