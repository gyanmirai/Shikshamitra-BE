using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.Expenses;
using ShikshaMitra.Finance.Incomes;
using ShikshaMitra.Finance.Setting;
using ShikshaMitra.Finance.Setting.ConcessionTypes;
using ShikshaMitra.Finance.StudentFeeAssignment;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
    // ───────────────────────────────────────────────
    // Finance — Fee Settings
    // ───────────────────────────────────────────────
    public DbSet<ShikshaMitra.Finance.Setting.FeeHead> FeeHeads { get; set; }
    public DbSet<ReceiptBook> ReceiptBooks { get; set; }
    public DbSet<FeeCategory> FeeCategories { get; set; }
    public DbSet<ShikshaMitra.Finance.Setting.FeeStructure.FeeStructure> FeeStructures { get; set; }
    public DbSet<ShikshaMitra.Finance.Setting.FeeStructure.FeeStructureStandard> FeeStructureStandards { get; set; }
    public DbSet<ShikshaMitra.Finance.Setting.FeeStructure.FeeStructureDetail> FeeStructureDetails { get; set; }

    // ───────────────────────────────────────────────
    // Finance — Concession Types
    // ───────────────────────────────────────────────
    public DbSet<ShikshaMitra.Finance.Setting.ConcessionType.ConcessionType> ConcessionTypes { get; set; }
    public DbSet<ShikshaMitra.Finance.Setting.ConcessionType.ConcessionTypeDocument> ConcessionTypeDocuments { get; set; }
    public DbSet<ConcessionTypeDocumentFormat> ConcessionTypeDocumentFormats { get; set; }

    // ───────────────────────────────────────────────
    // Finance — Categories & Bank
    // ───────────────────────────────────────────────
    public DbSet<ChargeCategory> ChargeCategories { get; set; }
    public DbSet<IncomeCategory> IncomeCategories { get; set; }
    public DbSet<ExpenseCategory> ExpenseCategories { get; set; }
    public DbSet<BankAccount> BankAccounts { get; set; }

    // ───────────────────────────────────────────────
    // Finance — Student Fee Assignment
    // ───────────────────────────────────────────────
    public DbSet<StudentFeeStructure> StudentFeeStructures { get; set; }

    // ───────────────────────────────────────────────
    // Finance — Concession Requests
    // ───────────────────────────────────────────────
    public DbSet<ConcessionRequest> ConcessionRequests { get; set; }
    public DbSet<ConcessionRequestConcessionType> ConcessionRequestConcessionTypes { get; set; }
    public DbSet<ConcessionRequestFeeHeadMapping> ConcessionRequestFeeHeadMappings { get; set; }
    public DbSet<ConcessionRequestFeeHeadValue> ConcessionRequestFeeHeadValues { get; set; }
    public DbSet<ConcessionRequestDocument> ConcessionRequestDocuments { get; set; }

    // ───────────────────────────────────────────────
    // Finance — Transactions
    // ───────────────────────────────────────────────
    public DbSet<ShikshaMitra.Finance.Charges.Charge> Charges { get; set; }
    public DbSet<Income> Incomes { get; set; }
    public DbSet<Expense> Expenses { get; set; }
}
