using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Collections;
using ShikshaMitra.Finance.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Receipts;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Seeds reconciliation inbox data so developers can immediately exercise every tab
/// and action on the Finance → Reconciliation screen after running DbMigrator.
///
/// Seeding strategy
/// ────────────────
/// • Idempotent: exits when marker receipt <c>KFE-RECON-CHEQUE</c> already exists.
/// • Depends on <see cref="FeeCollectionSeederContributor"/> (DueFees + fee heads).
/// • Runs after <see cref="FeeReceiptSeederContributor"/>.
/// • Covers all 7 non-cash payment-method tabs plus approved/rejected terminal states:
///
///   KFE-RECON-CHEQUE      Cheque              PendingVerification
///   KFE-RECON-DD          DemandDraft         PendingVerification
///   KFE-RECON-NEFT        BankTransfer        PendingVerification
///   KFE-RECON-CC          CreditCard          PendingVerification
///   KFE-RECON-UPI         UPI                 PendingVerification
///   KFE-RECON-OG          OnlineGateway       PendingVerification
///   KFE-RECON-WAL         Wallet              PendingVerification
///   KFE-RECON-BT-APPROVED BankTransfer        Verified  (+ FIN-RECON-BT-APPROVED)
///   KFE-RECON-UPI-REJECT  UPI                 Rejected  (metadata Reversed, DueFee restored)
///
/// Each transaction includes <see cref="FeeCollectionDueFeeAllocation"/> rows so the
/// reject flow can be exercised end-to-end.
/// </summary>
public class ReconciliationSeederContributor : BaseSeederContributor
{
    private const string MarkerReceiptNumber = "KFE-RECON-CHEQUE";
    private const string TuitionFeeHeadName  = "Seed – Monthly Tuition Fee";
    private const double SeedAmount          = 1_000d;

    private readonly IFeePaymentReceiptRepository              _receiptRepository;
    private readonly IRepository<FeeRecordMetadata, Guid>      _feeRecordMetadataRepository;
    private readonly IRepository<FeeCollectionRecord, Guid>    _feeCollectionRecordRepository;
    private readonly IRepository<PaymentMethodRecord, Guid>    _paymentMethodRecordRepository;
    private readonly IRepository<FeeCollectionDueFeeAllocation, Guid> _allocationRepository;
    private readonly IRepository<FeeHead, Guid>                _feeHeadRepository;
    private readonly IDueFeeRepository                         _dueFeeRepository;
    private readonly IRepository<Student, Guid>                _studentRepository;
    private readonly IAcademicYearRepository                   _academicYearRepository;
    private readonly IRepository<EducationBoard, Guid>         _educationBoardRepository;
    private readonly IEducationMediumRepository                  _educationMediumRepository;
    private readonly ILogger<ReconciliationSeederContributor>  _logger;
    private readonly IUnitOfWorkManager                    _unitOfWorkManager;

    public ReconciliationSeederContributor(
        ICurrentTenant currentTenant,
        IGuidGenerator guidGenerator,
        IUnitOfWork unitOfWork,
        IUnitOfWorkManager unitOfWorkManager,
        IFeePaymentReceiptRepository receiptRepository,
        IRepository<FeeRecordMetadata, Guid> feeRecordMetadataRepository,
        IRepository<FeeCollectionRecord, Guid> feeCollectionRecordRepository,
        IRepository<PaymentMethodRecord, Guid> paymentMethodRecordRepository,
        IRepository<FeeCollectionDueFeeAllocation, Guid> allocationRepository,
        IRepository<FeeHead, Guid> feeHeadRepository,
        IDueFeeRepository dueFeeRepository,
        IRepository<Student, Guid> studentRepository,
        IAcademicYearRepository academicYearRepository,
        IRepository<EducationBoard, Guid> educationBoardRepository,
        IEducationMediumRepository educationMediumRepository,
        ILogger<ReconciliationSeederContributor> logger)
        : base(currentTenant, guidGenerator, unitOfWork)
    {
        _receiptRepository              = receiptRepository;
        _feeRecordMetadataRepository    = feeRecordMetadataRepository;
        _feeCollectionRecordRepository  = feeCollectionRecordRepository;
        _paymentMethodRecordRepository  = paymentMethodRecordRepository;
        _allocationRepository           = allocationRepository;
        _feeHeadRepository              = feeHeadRepository;
        _dueFeeRepository               = dueFeeRepository;
        _studentRepository              = studentRepository;
        _academicYearRepository         = academicYearRepository;
        _educationBoardRepository       = educationBoardRepository;
        _educationMediumRepository      = educationMediumRepository;
        _logger                         = logger;
        _unitOfWorkManager              = unitOfWorkManager;
    }

    public override async Task SeedAsync(DataSeedContext context)
    {
        if (await _receiptRepository.FindByReceiptNumberAsync(MarkerReceiptNumber) is not null)
        {
            _logger.LogInformation(
                "[ReconciliationSeeder] Seed data already present ({Marker}). Skipping. TenantId={TenantId}",
                MarkerReceiptNumber, CurrentTenant.Id);
            return;
        }

        // Commit in an isolated UoW so data survives even if a later seeder throws.
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        await SeedCoreAsync(context);
        await uow.CompleteAsync();
    }

    private async Task SeedCoreAsync(DataSeedContext context)
    {
        _logger.LogInformation("[ReconciliationSeeder] Starting seed. TenantId={TenantId}", CurrentTenant.Id);

        var academicYear = await ResolveAcademicYearAsync();
        if (academicYear is null) return;

        var board = await _educationBoardRepository
            .FirstOrDefaultAsync(b => b.AcademicYearId == academicYear.Id);
        if (board is null)
        {
            _logger.LogWarning("[ReconciliationSeeder] No education board found — aborting.");
            return;
        }

        var medium = await _educationMediumRepository
            .FirstOrDefaultAsync(m => m.EducationBoardId == board.Id);
        if (medium is null)
        {
            _logger.LogWarning("[ReconciliationSeeder] No education medium found — aborting.");
            return;
        }

        var tuitionHead = (await _feeHeadRepository.GetListAsync(h => h.Name == TuitionFeeHeadName))
            .FirstOrDefault();
        if (tuitionHead is null)
        {
            _logger.LogWarning(
                "[ReconciliationSeeder] Fee head '{FeeHead}' not found — run FeeCollectionSeeder first. Aborting.",
                TuitionFeeHeadName);
            return;
        }

        var students = await _studentRepository.GetListAsync(
            s => s.AcademicYearId == academicYear.Id
              && s.Status == StudentLifeCycleStatusEnum.Enrolled);

        if (students.Count == 0)
        {
            _logger.LogWarning("[ReconciliationSeeder] No enrolled students found — aborting.");
            return;
        }

        // Prefer students with unpaid DueFees so allocations + reject rollback work correctly.
        var seedStudents = new List<Student>();
        foreach (var student in students)
        {
            if (await FindAllocatableDueFeeAsync(student.Id, tuitionHead.Id, SeedAmount) is not null)
                seedStudents.Add(student);
        }

        if (seedStudents.Count == 0)
        {
            _logger.LogWarning(
                "[ReconciliationSeeder] No students with outstanding DueFees — using all enrolled students.");
            seedStudents = students;
        }

        var receiptDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var paymentDate = DateTimeOffset.UtcNow.AddDays(-2);
        var studentIndex = 0;

        Student NextStudent() => seedStudents[studentIndex++ % seedStudents.Count];

        // ── 7 pending tabs ────────────────────────────────────────────────────

        await SeedPendingAsync(NextStudent(), tuitionHead.Id, academicYear.Id, board.Id, medium.Id,
            "REC-RECON-CHEQUE", "KFE-RECON-CHEQUE", PaymentMethodEnum.Cheque,
            new ChequeDetails("RECON-CHQ-001", "HDFC Bank", "Main Branch", receiptDate),
            receiptDate, paymentDate);

        await SeedPendingAsync(NextStudent(), tuitionHead.Id, academicYear.Id, board.Id, medium.Id,
            "REC-RECON-DD", "KFE-RECON-DD", PaymentMethodEnum.DemandDraft,
            new DemandDraftDetails("RECON-DD-001", "SBI", "City Branch", receiptDate),
            receiptDate, paymentDate);

        await SeedPendingAsync(NextStudent(), tuitionHead.Id, academicYear.Id, board.Id, medium.Id,
            "REC-RECON-NEFT", "KFE-RECON-NEFT", PaymentMethodEnum.BankTransfer,
            new BankTransferDetails("NEFT-RECON-001", "ICICI Bank", "ICIC0001234"),
            receiptDate, paymentDate);

        await SeedPendingAsync(NextStudent(), tuitionHead.Id, academicYear.Id, board.Id, medium.Id,
            "REC-RECON-CC", "KFE-RECON-CC", PaymentMethodEnum.CreditCard,
            new CreditCardDetails("CC-RECON-001", "4321", "HDFC Bank"),
            receiptDate, paymentDate);

        await SeedPendingAsync(NextStudent(), tuitionHead.Id, academicYear.Id, board.Id, medium.Id,
            "REC-RECON-UPI", "KFE-RECON-UPI", PaymentMethodEnum.UPI,
            new UpiDetails("UPI-RECON-001", "school.fees@okicici", "GPay"),
            receiptDate, paymentDate);

        await SeedPendingAsync(NextStudent(), tuitionHead.Id, academicYear.Id, board.Id, medium.Id,
            "REC-RECON-OG", "KFE-RECON-OG", PaymentMethodEnum.OnlinePaymentGateway,
            new OnlineGatewayDetails("OG-RECON-001", "Razorpay", "ORD-RECON-001"),
            receiptDate, paymentDate);

        await SeedPendingAsync(NextStudent(), tuitionHead.Id, academicYear.Id, board.Id, medium.Id,
            "REC-RECON-WAL", "KFE-RECON-WAL", PaymentMethodEnum.WalletPrepaidCard,
            new WalletDetails("WAL-RECON-001", "Paytm", "9876543210"),
            receiptDate, paymentDate);

        // ── Approved example (Bank Transfer) ──────────────────────────────────

        await SeedApprovedAsync(
            NextStudent(), tuitionHead.Id, academicYear.Id, board.Id, medium.Id,
            "REC-RECON-BT-OK", "KFE-RECON-BT-APPROVED", "FIN-RECON-BT-APPROVED",
            new BankTransferDetails("NEFT-RECON-APPROVED", "Axis Bank", "UTIB0001234"),
            receiptDate.AddDays(-5), paymentDate.AddDays(-5));

        // ── Rejected example (UPI bounce) ─────────────────────────────────────

        await SeedRejectedAsync(
            NextStudent(), tuitionHead.Id, academicYear.Id, board.Id, medium.Id,
            "REC-RECON-UPI-RJ", "KFE-RECON-UPI-REJECT",
            new UpiDetails("UPI-RECON-REJECT", "parent@paytm", "Paytm"),
            receiptDate.AddDays(-3), paymentDate.AddDays(-3),
            "UPI transaction failed — amount debited but not credited to school account.");

        await CurrentUnitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[ReconciliationSeeder] Seed completed. TenantId={TenantId}. " +
            "Created 7 pending + 1 approved + 1 rejected reconciliation entries.",
            CurrentTenant.Id);
    }

    // ── Scenario builders ──────────────────────────────────────────────────────

    private async Task SeedPendingAsync(
        Student student,
        Guid feeHeadId,
        Guid academicYearId,
        Guid boardId,
        Guid mediumId,
        string txnKey,
        string receiptNumber,
        PaymentMethodEnum method,
        PaymentDetails details,
        DateOnly receiptDate,
        DateTimeOffset paymentDate)
    {
        var (txnId, _, allocated, _) = await CreateCollectionWithAllocationAsync(
            student, feeHeadId, academicYearId, txnKey, SeedAmount, paymentDate, method, details);

        await InsertReceiptAsync(new FeePaymentReceipt(GuidGenerator.Create())
        {
            TenantId                   = CurrentTenant.Id,
            FeeCollectionTransactionId = txnId,
            StudentId                  = student.Id,
            AcademicYearId             = academicYearId,
            BoardId                    = boardId,
            EducationMediumId          = mediumId,
            ReceiptScope               = FeeReceiptScope.Fee,
            ReceiptKind                = FeeReceiptKind.ProvisionalFee,
            VerificationStatus         = FeeReceiptVerificationStatus.PendingVerification,
            TotalPaidAmount            = SeedAmount,
            ReceiptNumber              = receiptNumber,
            ReceiptDate                = receiptDate,
            Remarks                    = $"Seed — {method} pending bank/instrument verification",
        });

        _logger.LogDebug(
            "[ReconciliationSeeder] Pending {Method}: {Receipt} (txn={Txn}, allocated={Allocated})",
            method, receiptNumber, txnKey, allocated);
    }

    private async Task SeedApprovedAsync(
        Student student,
        Guid feeHeadId,
        Guid academicYearId,
        Guid boardId,
        Guid mediumId,
        string txnKey,
        string provisionalNumber,
        string finalNumber,
        PaymentDetails details,
        DateOnly receiptDate,
        DateTimeOffset paymentDate)
    {
        var (txnId, _, _, _) = await CreateCollectionWithAllocationAsync(
            student, feeHeadId, academicYearId, txnKey, SeedAmount, paymentDate,
            PaymentMethodEnum.BankTransfer, details);

        var provisional = await InsertReceiptAsync(new FeePaymentReceipt(GuidGenerator.Create())
        {
            TenantId                   = CurrentTenant.Id,
            FeeCollectionTransactionId = txnId,
            StudentId                  = student.Id,
            AcademicYearId             = academicYearId,
            BoardId                    = boardId,
            EducationMediumId          = mediumId,
            ReceiptScope               = FeeReceiptScope.Fee,
            ReceiptKind                = FeeReceiptKind.ProvisionalFee,
            VerificationStatus         = FeeReceiptVerificationStatus.PendingVerification,
            TotalPaidAmount            = SeedAmount,
            ReceiptNumber              = provisionalNumber,
            ReceiptDate                = receiptDate,
            Remarks                    = "Seed — Bank transfer cleared (approved example)",
        });

        var final = await InsertReceiptAsync(new FeePaymentReceipt(GuidGenerator.Create())
        {
            TenantId                   = CurrentTenant.Id,
            FeeCollectionTransactionId = txnId,
            StudentId                  = student.Id,
            AcademicYearId             = academicYearId,
            BoardId                    = boardId,
            EducationMediumId          = mediumId,
            ReceiptScope               = FeeReceiptScope.Fee,
            ReceiptKind                = FeeReceiptKind.FinalFee,
            VerificationStatus         = FeeReceiptVerificationStatus.NotApplicable,
            TotalPaidAmount            = SeedAmount,
            ReceiptNumber              = finalNumber,
            ReceiptDate                = receiptDate.AddDays(1),
            LinkedProvisionalReceiptId = provisional.Id,
            Remarks                    = "Seed — Final receipt after bank transfer approval",
        });

        provisional.MarkVerified(final.Id, "Verified against bank statement");
        await _receiptRepository.UpdateAsync(provisional, autoSave: false);

        _logger.LogDebug(
            "[ReconciliationSeeder] Approved BankTransfer: {Provisional} → {Final}",
            provisionalNumber, finalNumber);
    }

    private async Task SeedRejectedAsync(
        Student student,
        Guid feeHeadId,
        Guid academicYearId,
        Guid boardId,
        Guid mediumId,
        string txnKey,
        string receiptNumber,
        PaymentDetails details,
        DateOnly receiptDate,
        DateTimeOffset paymentDate,
        string rejectReason)
    {
        var (txnId, dueFeeId, allocated, metadata) = await CreateCollectionWithAllocationAsync(
            student, feeHeadId, academicYearId, txnKey, SeedAmount, paymentDate,
            PaymentMethodEnum.UPI, details);

        var provisional = await InsertReceiptAsync(new FeePaymentReceipt(GuidGenerator.Create())
        {
            TenantId                   = CurrentTenant.Id,
            FeeCollectionTransactionId = txnId,
            StudentId                  = student.Id,
            AcademicYearId             = academicYearId,
            BoardId                    = boardId,
            EducationMediumId          = mediumId,
            ReceiptScope               = FeeReceiptScope.Fee,
            ReceiptKind                = FeeReceiptKind.ProvisionalFee,
            VerificationStatus         = FeeReceiptVerificationStatus.PendingVerification,
            TotalPaidAmount            = SeedAmount,
            ReceiptNumber              = receiptNumber,
            ReceiptDate                = receiptDate,
            Remarks                    = "Seed — UPI payment pending verification",
        });

        // Simulate full financial rollback on rejection
        provisional.MarkRejected(rejectReason);
        await _receiptRepository.UpdateAsync(provisional, autoSave: false);

        metadata.MarkReversed(rejectReason);
        await _feeRecordMetadataRepository.UpdateAsync(metadata, autoSave: false);

        if (dueFeeId != Guid.Empty && allocated > 0)
        {
            var dueFee = await _dueFeeRepository.GetAsync(dueFeeId);
            dueFee.ReversePayment(allocated);
            await _dueFeeRepository.UpdateAsync(dueFee, autoSave: false);
        }

        _logger.LogDebug(
            "[ReconciliationSeeder] Rejected UPI: {Receipt} (txn reversed, DueFee restored={Restored})",
            receiptNumber, allocated > 0);
    }

    // ── Shared transaction builder ─────────────────────────────────────────────

    /// <summary>
    /// Creates FeeRecordMetadata + FeeCollectionRecord + PaymentMethodRecord +
    /// optional DueFee payment + FeeCollectionDueFeeAllocation.
    /// Returns (transactionId, dueFeeId, allocatedAmount, metadata).
    /// </summary>
    private async Task<(Guid TxnId, Guid DueFeeId, double Allocated, FeeRecordMetadata Metadata)>
        CreateCollectionWithAllocationAsync(
        Student student,
        Guid feeHeadId,
        Guid academicYearId,
        string txnKey,
        double amount,
        DateTimeOffset paymentDate,
        PaymentMethodEnum method,
        PaymentDetails details)
    {
        var existing = await _feeRecordMetadataRepository
            .FirstOrDefaultAsync(m => m.ReceiptNumber == txnKey);
        if (existing is not null)
            return (existing.Id, Guid.Empty, 0, existing);

        var dueFee = await FindAllocatableDueFeeAsync(student.Id, feeHeadId, amount);

        var metadata = new FeeRecordMetadata(GuidGenerator.Create())
        {
            TenantId       = CurrentTenant.Id,
            StudentId      = student.Id,
            AcademicYearId = academicYearId,
            TotalPaidFee   = amount,
            ReceiptNumber  = txnKey,
            Status         = FeeCollectionStatus.Paid,
            Remarks        = "Seed transaction for reconciliation seeder",
        };
        await _feeRecordMetadataRepository.InsertAsync(metadata, autoSave: false);

        await _feeCollectionRecordRepository.InsertAsync(new FeeCollectionRecord(GuidGenerator.Create())
        {
            TenantId            = CurrentTenant.Id,
            StudentId           = student.Id,
            FeeHeadId           = feeHeadId,
            Amount              = amount,
            PaymentDate         = paymentDate,
            FeeRecordMetadataId = metadata.Id,
        }, autoSave: false);

        await _paymentMethodRecordRepository.InsertAsync(PaymentMethodRecord.Create(
            studentId:           student.Id,
            feeRecordMetadataId: metadata.Id,
            amount:              (decimal)amount,
            paymentDate:         paymentDate,
            paymentMethod:       method,
            paymentDetails:      details,
            tenantId:            CurrentTenant.Id,
            notes:               $"Seed {method} payment for reconciliation"), autoSave: false);

        var allocated = 0d;
        var dueFeeId  = Guid.Empty;

        if (dueFee is not null)
        {
            allocated = Math.Min(amount, dueFee.OutstandingAmount);
            dueFee.ApplyPayment(allocated);
            await _dueFeeRepository.UpdateAsync(dueFee, autoSave: false);
            dueFeeId = dueFee.Id;

            await _allocationRepository.InsertAsync(new FeeCollectionDueFeeAllocation(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                metadata.Id,
                dueFee.Id,
                allocated), autoSave: false);
        }
        else
        {
            _logger.LogWarning(
                "[ReconciliationSeeder] No allocatable DueFee for student {StudentId} — " +
                "txn {TxnKey} seeded without allocation (list UI still works, reject rollback won't).",
                student.Id, txnKey);
        }

        return (metadata.Id, dueFeeId, allocated, metadata);
    }

    private async Task<DueFee?> FindAllocatableDueFeeAsync(Guid studentId, Guid feeHeadId, double amount)
    {
        var dueFees = await _dueFeeRepository.GetListAsync(
            d => d.StudentId == studentId && d.FeeHeadId == feeHeadId && d.OutstandingAmount > 0);

        return dueFees
            .OrderBy(d => d.Year)
            .ThenBy(d => d.Month)
            .FirstOrDefault(d => d.OutstandingAmount >= amount)
         ?? dueFees.FirstOrDefault();
    }

    private async Task<FeePaymentReceipt> InsertReceiptAsync(FeePaymentReceipt receipt)
    {
        var existing = await _receiptRepository.FindByReceiptNumberAsync(receipt.ReceiptNumber);
        if (existing is not null)
            return existing;

        await _receiptRepository.InsertAsync(receipt, autoSave: false);
        return receipt;
    }

    private async Task<AcademicYear?> ResolveAcademicYearAsync()
    {
        var year = DateTime.UtcNow.Year;
        return await _academicYearRepository.FirstOrDefaultAsync(y => y.Name.StartsWith(year.ToString()))
            ?? await _academicYearRepository.FirstOrDefaultAsync();
    }
}
