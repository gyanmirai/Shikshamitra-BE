using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Finance.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Receipts;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Seeds realistic <see cref="FeePaymentReceipt"/> data so developers can immediately
/// exercise the Receipts screen (list, filters, preview, download) without having to
/// manually run the collect flow.
///
/// Seeding strategy
/// ────────────────
/// • Idempotent: exits early when any seed receipt (number starts with "FIN-SEED" /
///   "KFE-SEED" / "KCH-SEED") already exists.
/// • Depends on <see cref="FeeCollectionSeederContributor"/> (FeeRecordMetadata rows +
///   enrolled students must already exist).
/// • Covers all three receipt kinds and verification states to exercise every UI path:
///
///   FIN-SEED-000001  FinalFee          Cash      student[0]  (June)     ← mirrors REC-SEED-000001
///   FIN-SEED-000002  FinalFee          UPI       student[0]  (July)     ← mirrors REC-SEED-000002
///   KFE-SEED-000003  ProvisionalFee    Cheque    student[1]  (June)     PendingVerification
///   KFE-SEED-000004  ProvisionalFee    DD        student[2]  (June)     PendingVerification (if ≥3 students)
///   KCH-SEED-000005  ProvisionalCharge Cash      student[0]  (charge)   NotApplicable
///   KCH-SEED-000006  ProvisionalCharge Cash      student[1]  same txn   fee+charge in one visit
///   FIN-SEED-000007  FinalFee          (verified) student[1]             LinkedProvisionalReceiptId = KFE-SEED-000003
///
/// Note: Receipts FIN-SEED-000001 and FIN-SEED-000002 are linked to the FeeRecordMetadata
/// rows created by FeeCollectionSeeder (REC-SEED-000001 / 002). All other receipts get
/// their own new FeeRecordMetadata transaction rows.
/// </summary>
public class FeeReceiptSeederContributor : BaseSeederContributor
{
    // ── Seed constants ────────────────────────────────────────────────────────

    private const double MonthlyFeeAmount  = 1_000d;   // matches FeeCollectionSeeder's ₹12,000/12
    private const double ChargeAmount      =   250d;
    private const double ChequeAmount      = 1_000d;

    // ── Dependencies ─────────────────────────────────────────────────────────

    private readonly IFeePaymentReceiptRepository                 _receiptRepository;
    private readonly IRepository<FeeRecordMetadata, Guid>         _feeRecordMetadataRepository;
    private readonly IRepository<PaymentMethodRecord, Guid>       _paymentMethodRecordRepository;
    private readonly IRepository<Student, Guid>                   _studentRepository;
    private readonly IAcademicYearRepository                      _academicYearRepository;
    private readonly IRepository<EducationBoard, Guid>            _educationBoardRepository;
    private readonly IEducationMediumRepository                   _educationMediumRepository;
    private readonly ILogger<FeeReceiptSeederContributor>         _logger;

    public FeeReceiptSeederContributor(
        ICurrentTenant currentTenant,
        IGuidGenerator guidGenerator,
        IUnitOfWork unitOfWork,
        IFeePaymentReceiptRepository receiptRepository,
        IRepository<FeeRecordMetadata, Guid> feeRecordMetadataRepository,
        IRepository<PaymentMethodRecord, Guid> paymentMethodRecordRepository,
        IRepository<Student, Guid> studentRepository,
        IAcademicYearRepository academicYearRepository,
        IRepository<EducationBoard, Guid> educationBoardRepository,
        IEducationMediumRepository educationMediumRepository,
        ILogger<FeeReceiptSeederContributor> logger)
        : base(currentTenant, guidGenerator, unitOfWork)
    {
        _receiptRepository           = receiptRepository;
        _feeRecordMetadataRepository = feeRecordMetadataRepository;
        _paymentMethodRecordRepository = paymentMethodRecordRepository;
        _studentRepository           = studentRepository;
        _academicYearRepository      = academicYearRepository;
        _educationBoardRepository    = educationBoardRepository;
        _educationMediumRepository   = educationMediumRepository;
        _logger                      = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────

    public override async Task SeedAsync(DataSeedContext context)
    {
        // ── Idempotency guard ─────────────────────────────────────────────────

        var existingSeedReceipt = await _receiptRepository
            .FindByReceiptNumberAsync("FIN-SEED-000001");

        if (existingSeedReceipt is not null)
        {
            _logger.LogInformation(
                "[FeeReceiptSeeder] Seed receipts already present. Skipping. TenantId={TenantId}",
                CurrentTenant.Id);
            return;
        }

        _logger.LogInformation("[FeeReceiptSeeder] Starting seed. TenantId={TenantId}", CurrentTenant.Id);

        // ── 1. Resolve academic context ───────────────────────────────────────

        var currentYear = DateTime.UtcNow.Year;
        var academicYear =
            await _academicYearRepository.FirstOrDefaultAsync(y => y.Name.StartsWith(currentYear.ToString()))
         ?? await _academicYearRepository.FirstOrDefaultAsync();

        if (academicYear is null)
        {
            _logger.LogWarning("[FeeReceiptSeeder] No academic year found — aborting.");
            return;
        }

        var board = await _educationBoardRepository
            .FirstOrDefaultAsync(b => b.AcademicYearId == academicYear.Id);
        if (board is null)
        {
            _logger.LogWarning("[FeeReceiptSeeder] No education board found — aborting.");
            return;
        }

        var medium = await _educationMediumRepository
            .FirstOrDefaultAsync(m => m.EducationBoardId == board.Id);
        if (medium is null)
        {
            _logger.LogWarning("[FeeReceiptSeeder] No education medium found — aborting.");
            return;
        }

        // ── 2. Resolve students ───────────────────────────────────────────────
        // Query only by academic year to avoid nullable-Guid mismatches on
        // AcademicBoardId / AcademicMediumId (both Guid? on Student).
        // We already hold the correct board.Id / medium.Id from the repository.

        var allStudents = await _studentRepository.GetListAsync(
            s => s.AcademicYearId == academicYear.Id);

        // Prefer students that match the resolved board/medium; fall back to any.
        var students = allStudents
            .Where(s => s.AcademicBoardId == board.Id && s.AcademicMediumId == medium.Id)
            .ToList();

        if (students.Count == 0)
            students = allStudents.Take(10).ToList();

        if (students.Count == 0)
        {
            _logger.LogWarning(
                "[FeeReceiptSeeder] No students found for AcademicYearId={AcademicYearId} — aborting.",
                academicYear.Id);
            return;
        }

        _logger.LogInformation("[FeeReceiptSeeder] Found {Count} students to use for seed receipts.", students.Count);

        var now       = DateTimeOffset.UtcNow;
        var receiptDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // ── 3. Find existing FeeCollectionSeeder transactions ─────────────────

        var txn1 = await _feeRecordMetadataRepository
            .FirstOrDefaultAsync(r => r.ReceiptNumber == "REC-SEED-000001");

        var txn2 = await _feeRecordMetadataRepository
            .FirstOrDefaultAsync(r => r.ReceiptNumber == "REC-SEED-000002");

        var student0 = students[0];
        var student1 = students.Count > 1 ? students[1] : students[0];
        var student2 = students.Count > 2 ? students[2] : students[0];

        // ── 4. FIN-SEED-000001 — FinalFee, Cash, student0 (mirrors REC-SEED-000001) ──

        var txn1Id = txn1?.Id ?? await CreateTransactionAsync(
            student0.Id, academicYear.Id, MonthlyFeeAmount, "FIN-SEED-000001", now.AddMonths(-2));

        var receipt1 = new FeePaymentReceipt(GuidGenerator.Create())
        {
            TenantId                   = CurrentTenant.Id,
            FeeCollectionTransactionId = txn1Id,
            StudentId                  = student0.Id,
            AcademicYearId             = academicYear.Id,
            BoardId                    = board.Id,
            EducationMediumId          = medium.Id,
            ReceiptScope               = FeeReceiptScope.Fee,
            ReceiptKind                = FeeReceiptKind.FinalFee,
            VerificationStatus         = FeeReceiptVerificationStatus.NotApplicable,
            TotalPaidAmount            = MonthlyFeeAmount,
            ReceiptNumber              = "FIN-SEED-000001",
            ReceiptDate                = DateOnly.FromDateTime(now.AddMonths(-2).DateTime),
            Remarks                    = "Seed — June tuition, Cash payment",
        };
        await _receiptRepository.InsertAsync(receipt1, autoSave: false);

        // ── 5. FIN-SEED-000002 — FinalFee, UPI, student0 (mirrors REC-SEED-000002) ───

        var txn2Id = txn2?.Id ?? await CreateTransactionAsync(
            student0.Id, academicYear.Id, MonthlyFeeAmount, "FIN-SEED-000002", now.AddMonths(-1));

        var receipt2 = new FeePaymentReceipt(GuidGenerator.Create())
        {
            TenantId                   = CurrentTenant.Id,
            FeeCollectionTransactionId = txn2Id,
            StudentId                  = student0.Id,
            AcademicYearId             = academicYear.Id,
            BoardId                    = board.Id,
            EducationMediumId          = medium.Id,
            ReceiptScope               = FeeReceiptScope.Fee,
            ReceiptKind                = FeeReceiptKind.FinalFee,
            VerificationStatus         = FeeReceiptVerificationStatus.NotApplicable,
            TotalPaidAmount            = MonthlyFeeAmount,
            ReceiptNumber              = "FIN-SEED-000002",
            ReceiptDate                = DateOnly.FromDateTime(now.AddMonths(-1).DateTime),
            Remarks                    = "Seed — July tuition, UPI payment",
        };
        await _receiptRepository.InsertAsync(receipt2, autoSave: false);

        // ── 6. KFE-SEED-000003 — ProvisionalFee, Cheque, student1 (PendingVerification) ──

        var txn3Id = await CreateTransactionAsync(
            student1.Id, academicYear.Id, ChequeAmount, "CHEQUE-SEED-000003", now.AddDays(-10));

        await CreateChequePaymentRecordAsync(
            student1.Id, txn3Id, ChequeAmount, now.AddDays(-10),
            chequeNo: "CHQ-100001", bank: "HDFC Bank", branch: "Main Branch",
            chequeDate: DateOnly.FromDateTime(now.AddDays(-10).DateTime));

        var receipt3 = new FeePaymentReceipt(GuidGenerator.Create())
        {
            TenantId                   = CurrentTenant.Id,
            FeeCollectionTransactionId = txn3Id,
            StudentId                  = student1.Id,
            AcademicYearId             = academicYear.Id,
            BoardId                    = board.Id,
            EducationMediumId          = medium.Id,
            ReceiptScope               = FeeReceiptScope.Fee,
            ReceiptKind                = FeeReceiptKind.ProvisionalFee,
            VerificationStatus         = FeeReceiptVerificationStatus.PendingVerification,
            TotalPaidAmount            = ChequeAmount,
            ReceiptNumber              = "KFE-SEED-000003",
            ReceiptDate                = DateOnly.FromDateTime(now.AddDays(-10).DateTime),
            Remarks                    = "Seed — Cheque payment pending bank clearance",
        };
        await _receiptRepository.InsertAsync(receipt3, autoSave: false);

        // ── 7. KFE-SEED-000004 — ProvisionalFee, DD, student2 (PendingVerification) ──

        var txn4Id = await CreateTransactionAsync(
            student2.Id, academicYear.Id, ChequeAmount, "DD-SEED-000004", now.AddDays(-7));

        await CreateDdPaymentRecordAsync(
            student2.Id, txn4Id, ChequeAmount, now.AddDays(-7),
            ddNo: "DD-200001", bank: "SBI", branch: "City Branch",
            ddDate: DateOnly.FromDateTime(now.AddDays(-7).DateTime));

        var receipt4 = new FeePaymentReceipt(GuidGenerator.Create())
        {
            TenantId                   = CurrentTenant.Id,
            FeeCollectionTransactionId = txn4Id,
            StudentId                  = student2.Id,
            AcademicYearId             = academicYear.Id,
            BoardId                    = board.Id,
            EducationMediumId          = medium.Id,
            ReceiptScope               = FeeReceiptScope.Fee,
            ReceiptKind                = FeeReceiptKind.ProvisionalFee,
            VerificationStatus         = FeeReceiptVerificationStatus.PendingVerification,
            TotalPaidAmount            = ChequeAmount,
            ReceiptNumber              = "KFE-SEED-000004",
            ReceiptDate                = DateOnly.FromDateTime(now.AddDays(-7).DateTime),
            Remarks                    = "Seed — Demand Draft pending bank clearance",
        };
        await _receiptRepository.InsertAsync(receipt4, autoSave: false);

        // ── 8. Shared transaction with BOTH fee + charge lines (student0) ─────
        //      Produces TWO receipts (KCH-SEED-000005 + FIN-SEED-000005-FEE)
        //      from the same FeeCollectionTransactionId — tests the "two rows per visit" scenario.

        var txn5Id = await CreateTransactionAsync(
            student0.Id, academicYear.Id, MonthlyFeeAmount + ChargeAmount,
            "MIXED-SEED-000005", now.AddDays(-3));

        await CreateCashPaymentRecordAsync(student0.Id, txn5Id, MonthlyFeeAmount + ChargeAmount, now.AddDays(-3));

        var receipt5fee = new FeePaymentReceipt(GuidGenerator.Create())
        {
            TenantId                   = CurrentTenant.Id,
            FeeCollectionTransactionId = txn5Id,
            StudentId                  = student0.Id,
            AcademicYearId             = academicYear.Id,
            BoardId                    = board.Id,
            EducationMediumId          = medium.Id,
            ReceiptScope               = FeeReceiptScope.Fee,
            ReceiptKind                = FeeReceiptKind.FinalFee,
            VerificationStatus         = FeeReceiptVerificationStatus.NotApplicable,
            TotalPaidAmount            = MonthlyFeeAmount,
            ReceiptNumber              = "FIN-SEED-000005",
            ReceiptDate                = receiptDate,
            Remarks                    = "Seed — August tuition, mixed visit (fee portion)",
        };
        await _receiptRepository.InsertAsync(receipt5fee, autoSave: false);

        var receipt5charge = new FeePaymentReceipt(GuidGenerator.Create())
        {
            TenantId                   = CurrentTenant.Id,
            FeeCollectionTransactionId = txn5Id,
            StudentId                  = student0.Id,
            AcademicYearId             = academicYear.Id,
            BoardId                    = board.Id,
            EducationMediumId          = medium.Id,
            ReceiptScope               = FeeReceiptScope.Charge,
            ReceiptKind                = FeeReceiptKind.ProvisionalCharge,
            VerificationStatus         = FeeReceiptVerificationStatus.NotApplicable,
            TotalPaidAmount            = ChargeAmount,
            ReceiptNumber              = "KCH-SEED-000005",
            ReceiptDate                = receiptDate,
            Remarks                    = "Seed — Activity charge, same visit as FIN-SEED-000005",
        };
        await _receiptRepository.InsertAsync(receipt5charge, autoSave: false);

        // ── 9. KCH-SEED-000006 — Standalone ProvisionalCharge, student1 ───────

        var txn6Id = await CreateTransactionAsync(
            student1.Id, academicYear.Id, ChargeAmount, "CHARGE-SEED-000006", now.AddDays(-5));

        await CreateCashPaymentRecordAsync(student1.Id, txn6Id, ChargeAmount, now.AddDays(-5));

        var receipt6 = new FeePaymentReceipt(GuidGenerator.Create())
        {
            TenantId                   = CurrentTenant.Id,
            FeeCollectionTransactionId = txn6Id,
            StudentId                  = student1.Id,
            AcademicYearId             = academicYear.Id,
            BoardId                    = board.Id,
            EducationMediumId          = medium.Id,
            ReceiptScope               = FeeReceiptScope.Charge,
            ReceiptKind                = FeeReceiptKind.ProvisionalCharge,
            VerificationStatus         = FeeReceiptVerificationStatus.NotApplicable,
            TotalPaidAmount            = ChargeAmount,
            ReceiptNumber              = "KCH-SEED-000006",
            ReceiptDate                = receiptDate.AddDays(-5),
            Remarks                    = "Seed — Library charge, standalone charge receipt",
        };
        await _receiptRepository.InsertAsync(receipt6, autoSave: false);

        // ── 10. FIN-SEED-000007 — Reconciled FinalFee: kachi KFE-SEED-000003 verified ──
        //       Demonstrates the full lifecycle: kachi → verified → final receipt issued.
        //       Reuses txn3Id (same transaction) — this is how reconciliation works.

        var finalReceipt = new FeePaymentReceipt(GuidGenerator.Create())
        {
            TenantId                        = CurrentTenant.Id,
            FeeCollectionTransactionId      = txn3Id,   // same transaction as KFE-SEED-000003
            StudentId                       = student1.Id,
            AcademicYearId                  = academicYear.Id,
            BoardId                         = board.Id,
            EducationMediumId               = medium.Id,
            ReceiptScope                    = FeeReceiptScope.Fee,
            ReceiptKind                     = FeeReceiptKind.FinalFee,
            VerificationStatus              = FeeReceiptVerificationStatus.NotApplicable,
            TotalPaidAmount                 = ChequeAmount,
            ReceiptNumber                   = "FIN-SEED-000007",
            ReceiptDate                     = receiptDate.AddDays(-2),
            LinkedProvisionalReceiptId      = receipt3.Id,
            Remarks                         = "Seed — Final receipt after cheque KFE-SEED-000003 cleared",
        };
        await _receiptRepository.InsertAsync(finalReceipt, autoSave: false);

        // Update the kachi receipt to mark it verified and link to the final receipt
        receipt3.MarkVerified(finalReceipt.Id);
        await _receiptRepository.UpdateAsync(receipt3, autoSave: false);

        // Save all changes in one batch
        await CurrentUnitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[FeeReceiptSeeder] Seeded 7 receipts across 3 kinds for tenant {TenantId}. " +
            "Receipts: FIN×3, KFE×2(1 verified), KCH×2. " +
            "FIN-SEED-000005 + KCH-SEED-000005 share same transaction (fee+charge split).",
            CurrentTenant.Id);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Creates a minimal <see cref="FeeRecordMetadata"/> transaction row to back
    /// any receipt that does not already have a corresponding seeded transaction.
    /// Returns the new transaction ID.
    /// </summary>
    private async Task<Guid> CreateTransactionAsync(
        Guid studentId,
        Guid academicYearId,
        double totalPaid,
        string receiptNumber,
        DateTimeOffset paymentDate)
    {
        var metadata = new FeeRecordMetadata(GuidGenerator.Create())
        {
            TenantId       = CurrentTenant.Id,
            StudentId      = studentId,
            AcademicYearId = academicYearId,
            TotalPaidFee   = totalPaid,
            ReceiptNumber  = receiptNumber,
            Status         = FeeCollectionStatus.Paid,
            Remarks        = "Seed transaction for receipt seeder",
        };
        await _feeRecordMetadataRepository.InsertAsync(metadata, autoSave: false);
        return metadata.Id;
    }

    private async Task CreateCashPaymentRecordAsync(
        Guid studentId, Guid txnId, double amount, DateTimeOffset paymentDate)
    {
        var record = PaymentMethodRecord.Create(
            studentId:           studentId,
            feeRecordMetadataId: txnId,
            amount:              (decimal)amount,
            paymentDate:         paymentDate,
            paymentMethod:       PaymentMethodEnum.Cash,
            paymentDetails:      null,
            tenantId:            CurrentTenant.Id,
            notes:               "Seed cash payment");
        await _paymentMethodRecordRepository.InsertAsync(record, autoSave: false);
    }

    private async Task CreateChequePaymentRecordAsync(
        Guid studentId, Guid txnId, double amount, DateTimeOffset paymentDate,
        string chequeNo, string bank, string branch, DateOnly chequeDate)
    {
        var record = PaymentMethodRecord.Create(
            studentId:           studentId,
            feeRecordMetadataId: txnId,
            amount:              (decimal)amount,
            paymentDate:         paymentDate,
            paymentMethod:       PaymentMethodEnum.Cheque,
            paymentDetails:      new ChequeDetails(chequeNo, bank, branch, chequeDate),
            tenantId:            CurrentTenant.Id,
            notes:               "Seed cheque payment — pending bank clearance");
        await _paymentMethodRecordRepository.InsertAsync(record, autoSave: false);
    }

    private async Task CreateDdPaymentRecordAsync(
        Guid studentId, Guid txnId, double amount, DateTimeOffset paymentDate,
        string ddNo, string bank, string branch, DateOnly ddDate)
    {
        var record = PaymentMethodRecord.Create(
            studentId:           studentId,
            feeRecordMetadataId: txnId,
            amount:              (decimal)amount,
            paymentDate:         paymentDate,
            paymentMethod:       PaymentMethodEnum.DemandDraft,
            paymentDetails:      new DemandDraftDetails(ddNo, bank, branch, ddDate),
            tenantId:            CurrentTenant.Id,
            notes:               "Seed demand draft payment — pending bank clearance");
        await _paymentMethodRecordRepository.InsertAsync(record, autoSave: false);
    }
}
