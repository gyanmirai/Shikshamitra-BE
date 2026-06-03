using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.FeeRefund;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students.Fees;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	// ───────────────────────────────────────────────
	// Finance — Fee Settings
	// ───────────────────────────────────────────────
	public DbSet<FeeHead> FeeHeads { get; set; }
	public DbSet<ReceiptBook> ReceiptBooks { get; set; }
	public DbSet<FeeCategory> FeeCategories { get; set; }
	public DbSet<FeeStructureMetadata> FeeStructureMetadatas { get; set; }
	public DbSet<FeeStructure> FeeStructures { get; set; }
	public DbSet<FeeStructureDetail> FeeStructureDetails { get; set; }

	// ───────────────────────────────────────────────
	// Finance — Concession Requests
	// ───────────────────────────────────────────────
	public DbSet<ConcessionRequest> ConcessionRequests { get; set; }
	public DbSet<ConcessionFeeHeadMapping> ConcessionFeeHeadMappings { get; set; }
	public DbSet<ConcessionRequestDocument> ConcessionRequestDocuments { get; set; }
	public DbSet<ConcessionDueFeeAllocation> ConcessionDueFeeAllocations { get; set; }

	// ───────────────────────────────────────────────
	// Finance — Concession Types
	// ───────────────────────────────────────────────
	public DbSet<ConcessionType> ConcessionTypes { get; set; }
	public DbSet<ConcessionTypeDocument> ConcessionTypeDocuments { get; set; }
	public DbSet<ConcessionTypeDocumentFormat> ConcessionTypeDocumentFormats { get; set; }

	// ───────────────────────────────────────────────
	// Finance — Categories & Bank
	// ───────────────────────────────────────────────
	public DbSet<ChargeCategory> ChargeCategories { get; set; }
	public DbSet<TransactionCategory> TransactionCategories { get; set; }
	public DbSet<BankAccount> BankAccounts { get; set; }

	// ───────────────────────────────────────────────
	// Finance — Student Fee Assignment
	// ───────────────────────────────────────────────
	public DbSet<StudentFeeStructure> StudentFeeStructures { get; set; }

	// ───────────────────────────────────────────────
	// Finance — Transactions
	// ───────────────────────────────────────────────
	public DbSet<Charge> Charges { get; set; }
	public DbSet<Transaction> Transactions { get; set; }

	// ───────────────────────────────────────────────
	// Finance — Fee Collection
	// ───────────────────────────────────────────────
	public DbSet<DueFee> DueFees { get; set; }
	public DbSet<FeeRecordMetadata> FeeRecordMetadatas { get; set; }
	public DbSet<FeeCollectionRecord> FeeCollectionRecords { get; set; }
	public DbSet<ChargeCollectionRecord> ChargeCollectionRecords { get; set; }
	public DbSet<PaymentMethodRecord> PaymentMethodRecords { get; set; }
	public DbSet<StudentFeeRecord> StudentFeeRecords { get; set; }
	public DbSet<FeePaymentReceipt> FeePaymentReceipts { get; set; }
	public DbSet<FeeCollectionDueFeeAllocation> FeeCollectionDueFeeAllocations { get; set; }
	public DbSet<ReceiptNumberSequence> ReceiptNumberSequences { get; set; }

	// ───────────────────────────────────────────────
	// Finance — Fee Refunds
	// ───────────────────────────────────────────────
	public DbSet<FeeRefundRequest> FeeRefundRequests { get; set; }
}
