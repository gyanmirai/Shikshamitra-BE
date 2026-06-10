using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using ShikshaMitra.Finance.Fees.Collections;

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

        var now         = DateTimeOffset.UtcNow;
        var receiptDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var student0 = students[0];
        var student1 = students.Count > 1 ? students[1] : students[0];
        var student2 = students.Count > 2 ? students[2] : students[0];

        // ── 3. Pre-load ALL existing seed transactions in one query ───────────
        // Doing this upfront (before any write) means the DbContext is in a clean
        // state and there is no risk of querying on a broken transaction later.

        var allSeedKeys = new List<string>
        {
            "REC-SEED-000001", "REC-SEED-000002",       // FeeCollectionSeeder keys
            "FIN-SEED-000001", "FIN-SEED-000002",       // receipt-seeder fallback keys
            "CHEQUE-SEED-000003", "DD-SEED-000004",
            "MIXED-SEED-000005", "CHARGE-SEED-000006",
        };

        var existingTxns = (await _feeRecordMetadataRepository
            .GetListAsync(r => allSeedKeys.Contains(r.ReceiptNumber)))
            .ToDictionary(r => r.ReceiptNumber, r => r.Id);

        Guid ResolveOrMarkMissing(string key) =>
            existingTxns.TryGetValue(key, out var id) ? id : Guid.Empty;

        // ── 4. FIN-SEED-000001 — FinalFee, Cash, student0 (mirrors REC-SEED-000001) ──

        // Prefer the FeeCollectionSeeder transaction; fall back to our own key.
        var txn1Id = ResolveOrMarkMissing("REC-SEED-000001") is var r1 && r1 != Guid.Empty
            ? r1
            : await CreateTransactionAsync(student0.Id, academicYear.Id, MonthlyFeeAmount,
                "FIN-SEED-000001", now.AddMonths(-2), existingTxns);

        await InsertIfNotExistsAsync("FIN-SEED-000001", new FeePaymentReceipt(GuidGenerator.Create())
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
        });

        // ── 5. FIN-SEED-000002 — FinalFee, UPI, student0 (mirrors REC-SEED-000002) ───

        var txn2Id = ResolveOrMarkMissing("REC-SEED-000002") is var r2 && r2 != Guid.Empty
            ? r2
            : await CreateTransactionAsync(student0.Id, academicYear.Id, MonthlyFeeAmount,
                "FIN-SEED-000002", now.AddMonths(-1), existingTxns);

        await InsertIfNotExistsAsync("FIN-SEED-000002", new FeePaymentReceipt(GuidGenerator.Create())
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
        });

        // ── 6. KFE-SEED-000003 — ProvisionalFee, Cheque, student1 (PendingVerification) ──

        var txn3Id = await CreateTransactionAsync(
            student1.Id, academicYear.Id, ChequeAmount, "CHEQUE-SEED-000003", now.AddDays(-10), existingTxns);

        await CreateChequePaymentRecordIfAbsentAsync(
            "CHEQUE-SEED-000003",
            student1.Id, txn3Id, ChequeAmount, now.AddDays(-10),
            chequeNo: "CHQ-100001", bank: "HDFC Bank", branch: "Main Branch",
            chequeDate: DateOnly.FromDateTime(now.AddDays(-10).DateTime));

        var receipt3 = await _receiptRepository.FindByReceiptNumberAsync("KFE-SEED-000003")
            ?? await InsertReceiptAsync(new FeePaymentReceipt(GuidGenerator.Create())
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
            });

        // ── 7. KFE-SEED-000004 — ProvisionalFee, DD, student2 (PendingVerification) ──

        var txn4Id = await CreateTransactionAsync(
            student2.Id, academicYear.Id, ChequeAmount, "DD-SEED-000004", now.AddDays(-7), existingTxns);

        await CreateDdPaymentRecordIfAbsentAsync(
            "DD-SEED-000004",
            student2.Id, txn4Id, ChequeAmount, now.AddDays(-7),
            ddNo: "DD-200001", bank: "SBI", branch: "City Branch",
            ddDate: DateOnly.FromDateTime(now.AddDays(-7).DateTime));

        await InsertIfNotExistsAsync("KFE-SEED-000004", new FeePaymentReceipt(GuidGenerator.Create())
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
        });

        // ── 8. Shared transaction: fee + charge from one visit (student0) ─────

        var txn5Id = await CreateTransactionAsync(
            student0.Id, academicYear.Id, MonthlyFeeAmount + ChargeAmount,
            "MIXED-SEED-000005", now.AddDays(-3), existingTxns);

        await CreateCashPaymentRecordIfAbsentAsync(
            "MIXED-SEED-000005", student0.Id, txn5Id,
            MonthlyFeeAmount + ChargeAmount, now.AddDays(-3));

        await InsertIfNotExistsAsync("FIN-SEED-000005", new FeePaymentReceipt(GuidGenerator.Create())
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
        });

        await InsertIfNotExistsAsync("KCH-SEED-000005", new FeePaymentReceipt(GuidGenerator.Create())
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
        });

        // ── 9. KCH-SEED-000006 — Standalone ProvisionalCharge, student1 ───────

        var txn6Id = await CreateTransactionAsync(
            student1.Id, academicYear.Id, ChargeAmount,
            "CHARGE-SEED-000006", now.AddDays(-5), existingTxns);

        await CreateCashPaymentRecordIfAbsentAsync(
            "CHARGE-SEED-000006", student1.Id, txn6Id, ChargeAmount, now.AddDays(-5));

        await InsertIfNotExistsAsync("KCH-SEED-000006", new FeePaymentReceipt(GuidGenerator.Create())
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
        });

        // ── 10. FIN-SEED-000007 — Reconciled FinalFee (KFE-SEED-000003 verified) ──

        if (await _receiptRepository.FindByReceiptNumberAsync("FIN-SEED-000007") is null)
        {
            var finalReceipt = await InsertReceiptAsync(new FeePaymentReceipt(GuidGenerator.Create())
            {
                TenantId                   = CurrentTenant.Id,
                FeeCollectionTransactionId = txn3Id,
                StudentId                  = student1.Id,
                AcademicYearId             = academicYear.Id,
                BoardId                    = board.Id,
                EducationMediumId          = medium.Id,
                ReceiptScope               = FeeReceiptScope.Fee,
                ReceiptKind                = FeeReceiptKind.FinalFee,
                VerificationStatus         = FeeReceiptVerificationStatus.NotApplicable,
                TotalPaidAmount            = ChequeAmount,
                ReceiptNumber              = "FIN-SEED-000007",
                ReceiptDate                = receiptDate.AddDays(-2),
                LinkedProvisionalReceiptId = receipt3.Id,
                Remarks                    = "Seed — Final receipt after cheque KFE-SEED-000003 cleared",
            });

            if (receipt3.VerificationStatus != FeeReceiptVerificationStatus.Verified)
            {
                receipt3.MarkVerified(finalReceipt.Id);
                await _receiptRepository.UpdateAsync(receipt3, autoSave: true);
            }
        }

        _logger.LogInformation(
            "[FeeReceiptSeeder] Seed completed for tenant {TenantId}. " +
            "Receipts: FIN×3, KFE×2(1 verified), KCH×2. " +
            "FIN-SEED-000005 + KCH-SEED-000005 share the same transaction.",
            CurrentTenant.Id);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    /// <summary>Inserts the receipt only if no receipt with that number exists yet.</summary>
    private async Task InsertIfNotExistsAsync(string receiptNumber, FeePaymentReceipt receipt)
    {
        if (await _receiptRepository.FindByReceiptNumberAsync(receiptNumber) is not null)
        {
            _logger.LogDebug("[FeeReceiptSeeder] Receipt {Number} already exists — skipping.", receiptNumber);
            return;
        }
        await _receiptRepository.InsertAsync(receipt, autoSave: true);
    }

    /// <summary>
    /// Inserts the receipt and returns it (used when the ID is needed for linking).
    /// Returns the already-committed row if it exists.
    /// </summary>
    private async Task<FeePaymentReceipt> InsertReceiptAsync(FeePaymentReceipt receipt)
    {
        var existing = await _receiptRepository.FindByReceiptNumberAsync(receipt.ReceiptNumber);
        if (existing is not null)
        {
            _logger.LogDebug("[FeeReceiptSeeder] Receipt {Number} already exists — reusing.", receipt.ReceiptNumber);
            return existing;
        }
        await _receiptRepository.InsertAsync(receipt, autoSave: true);
        return receipt;
    }

    /// <summary>
    /// Returns the ID for <paramref name="seedKey"/>: uses the preloaded <paramref name="existingTxns"/>
    /// dictionary when available (no DB round-trip), otherwise inserts a new row.
    /// Preloading all keys at the start of <see cref="SeedAsync"/> guarantees the DbContext
    /// is clean when this is called, eliminating the broken-transaction race condition.
    /// </summary>
    private async Task<Guid> CreateTransactionAsync(
        Guid studentId,
        Guid academicYearId,
        double totalPaid,
        string seedKey,
        DateTimeOffset paymentDate,
        Dictionary<string, Guid> existingTxns)
    {
        if (existingTxns.TryGetValue(seedKey, out var existingId))
        {
            _logger.LogDebug("[FeeReceiptSeeder] Transaction {Key} already exists — reusing.", seedKey);
            return existingId;
        }

        var metadata = new FeeRecordMetadata(GuidGenerator.Create())
        {
            TenantId       = CurrentTenant.Id,
            StudentId      = studentId,
            AcademicYearId = academicYearId,
            TotalPaidFee   = totalPaid,
            ReceiptNumber  = seedKey,
            Status         = FeeCollectionStatus.Paid,
            Remarks        = "Seed transaction for receipt seeder",
        };
        await _feeRecordMetadataRepository.InsertAsync(metadata, autoSave: true);

        // Add to cache so subsequent calls within this run don't re-query.
        existingTxns[seedKey] = metadata.Id;
        return metadata.Id;
    }

    private async Task CreateCashPaymentRecordIfAbsentAsync(
        string txnSeedKey, Guid studentId, Guid txnId, double amount, DateTimeOffset paymentDate)
    {
        if (await _paymentMethodRecordRepository.AnyAsync(
                r => r.FeeRecordMetadataId == txnId && r.PaymentMethod == PaymentMethodEnum.Cash))
            return;

        await _paymentMethodRecordRepository.InsertAsync(PaymentMethodRecord.Create(
            studentId:           studentId,
            feeRecordMetadataId: txnId,
            amount:              (decimal)amount,
            paymentDate:         paymentDate,
            paymentMethod:       PaymentMethodEnum.Cash,
            paymentDetails:      null,
            tenantId:            CurrentTenant.Id,
            notes:               "Seed cash payment"), autoSave: true);
    }

    private async Task CreateChequePaymentRecordIfAbsentAsync(
        string txnSeedKey, Guid studentId, Guid txnId, double amount, DateTimeOffset paymentDate,
        string chequeNo, string bank, string branch, DateOnly chequeDate)
    {
        if (await _paymentMethodRecordRepository.AnyAsync(
                r => r.FeeRecordMetadataId == txnId && r.PaymentMethod == PaymentMethodEnum.Cheque))
            return;

        await _paymentMethodRecordRepository.InsertAsync(PaymentMethodRecord.Create(
            studentId:           studentId,
            feeRecordMetadataId: txnId,
            amount:              (decimal)amount,
            paymentDate:         paymentDate,
            paymentMethod:       PaymentMethodEnum.Cheque,
            paymentDetails:      new ChequeDetails(chequeNo, bank, branch, chequeDate),
            tenantId:            CurrentTenant.Id,
            notes:               "Seed cheque payment — pending bank clearance"), autoSave: true);
    }

    private async Task CreateDdPaymentRecordIfAbsentAsync(
        string txnSeedKey, Guid studentId, Guid txnId, double amount, DateTimeOffset paymentDate,
        string ddNo, string bank, string branch, DateOnly ddDate)
    {
        if (await _paymentMethodRecordRepository.AnyAsync(
                r => r.FeeRecordMetadataId == txnId && r.PaymentMethod == PaymentMethodEnum.DemandDraft))
            return;

        await _paymentMethodRecordRepository.InsertAsync(PaymentMethodRecord.Create(
            studentId:           studentId,
            feeRecordMetadataId: txnId,
            amount:              (decimal)amount,
            paymentDate:         paymentDate,
            paymentMethod:       PaymentMethodEnum.DemandDraft,
            paymentDetails:      new DemandDraftDetails(ddNo, bank, branch, ddDate),
            tenantId:            CurrentTenant.Id,
            notes:               "Seed demand draft payment — pending bank clearance"), autoSave: true);
    }
}
