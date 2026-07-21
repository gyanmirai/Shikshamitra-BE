using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Students.Fees;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Aggregates;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Entities;
using ShikshaMitra.Domain.Finance.Policies;

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
	public DbSet<ChargeRequest> ChargeRequests { get; set; }
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
	public DbSet<LateFeeCollectionAllocation> LateFeeCollectionAllocations { get; set; }
	public DbSet<LateFeeWaiverApplication> LateFeeWaiverApplications { get; set; }

	// ───────────────────────────────────────────────
	// Finance — Fee Refunds
	// ───────────────────────────────────────────────
	public DbSet<FeeRefundRequest> FeeRefundRequests { get; set; }

	// ───────────────────────────────────────────────
	// Finance — Fee Adjustments
	// ───────────────────────────────────────────────
	public DbSet<FeeAdjustmentRequest> FeeAdjustmentRequests { get; set; }
	public DbSet<FeeAdjustmentSourceLine> FeeAdjustmentSourceLines { get; set; }
	public DbSet<FeeAdjustmentTargetAllocation> FeeAdjustmentTargetAllocations { get; set; }

	// ───────────────────────────────────────────────
	// Finance — Audit Trail
	// ───────────────────────────────────────────────
	public DbSet<FinanceAuditEvent> FinanceAuditEvents { get; set; }

	// ───────────────────────────────────────────────
	// Finance — Policies
	// ───────────────────────────────────────────────
	public DbSet<CollectionPolicy> CollectionPolicies { get; set; }
	public DbSet<FinancePolicyVersion> FinancePolicyVersions { get; set; }
	public DbSet<StudentFeePolicyOverride> StudentFeePolicyOverrides { get; set; }
	public DbSet<CollectionPolicyExceptionWindow> CollectionPolicyExceptionWindows { get; set; }
}
