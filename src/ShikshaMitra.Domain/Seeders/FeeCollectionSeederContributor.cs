using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Onboarding.AcademicYears;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Seeds realistic fee collection data so developers can immediately exercise the full
/// fee collection UI flow (summary list, collect form, student fee details, export)
/// without manually configuring fee structures, assigning students, or creating receipts.
///
/// Seeding strategy
/// ────────────────
/// • Idempotent: exits early when any <see cref="FeeRecordMetadata"/> already exists.
/// • Depends on <see cref="StudentSeederContributor"/> (enrolled students must exist).
/// • Depends on <see cref="AcademicSetupSeederContributor"/> (academic setup must exist).
/// • Inserts in order:
///     1. "Seed – School Fees" fee category + two fee heads (Tuition, Development)
///     2. A receipt book for the first board/medium combination
///     3. One FeeStructureMetadata for the first board/medium/year
///     4. One FeeStructure per standard (up to <see cref="MaxStructuredStandards"/>)
///     5. FeeStructureDetails: monthly tuition + one-time development fee per structure
///     6. StudentFeeStructure assignments for enrolled students
///     7. DueFee records for each newly assigned student (skips students that already
///        have DueFees from <see cref="FeeRefundSeederContributor"/>)
///     8. Three receipts showcasing different payment scenarios:
///        – REC-SEED-000001  Cash  — June fully paid     (student 0)
///        – REC-SEED-000002  UPI   — July fully paid     (student 0)
///        – REC-SEED-000003  Cash  — June partial ₹500   (student 1)
/// </summary>
public class FeeCollectionSeederContributor : BaseSeederContributor
{
    // ── Seed constants ────────────────────────────────────────────────────────

    private const string FeeCategoryName    = "Seed – School Fees";
    private const string TuitionFeeHeadName = "Seed – Monthly Tuition Fee";
    private const string DevFeeHeadName     = "Seed – Development Fee";

    private const double AnnualTuitionFee   = 12_000d;   // ₹1,000/month × 12
    private const double DevFeeAmount       = 2_500d;    // one-time

    /// <summary>Maximum number of standards for which fee structures are created.</summary>
    private const int MaxStructuredStandards = 5;

    // ── Dependencies ─────────────────────────────────────────────────────────

    private readonly IAcademicYearRepository                         _academicYearRepository;
    private readonly IRepository<EducationBoard, Guid>               _educationBoardRepository;
    private readonly IEducationMediumRepository                      _educationMediumRepository;
    private readonly IEducationMediumStageRepository                 _educationMediumStageRepository;
    private readonly IRepository<Standard, Guid>                     _standardRepository;
    private readonly IRepository<Student, Guid>                      _studentRepository;
    private readonly IRepository<FeeCategory, Guid>                  _feeCategoryRepository;
    private readonly IRepository<FeeHead, Guid>                      _feeHeadRepository;
    private readonly IRepository<ReceiptBook, Guid>                  _receiptBookRepository;
    private readonly IRepository<FeeStructureMetadata, Guid>         _feeStructureMetadataRepository;
    private readonly IRepository<FeeStructure, Guid>                 _feeStructureRepository;
    private readonly IRepository<FeeStructureDetail, Guid>           _feeStructureDetailRepository;
    private readonly IRepository<StudentFeeStructure, Guid>          _studentFeeStructureRepository;
    private readonly IDueFeeRepository                               _dueFeeRepository;
    private readonly IRepository<FeeRecordMetadata, Guid>            _feeRecordMetadataRepository;
    private readonly IRepository<FeeCollectionRecord, Guid>          _feeCollectionRecordRepository;
    private readonly IRepository<PaymentMethodRecord, Guid>          _paymentMethodRecordRepository;
    private readonly ILogger<FeeCollectionSeederContributor>         _logger;

    public FeeCollectionSeederContributor(
        ICurrentTenant currentTenant,
        IGuidGenerator guidGenerator,
        IUnitOfWork unitOfWork,
        IAcademicYearRepository academicYearRepository,
        IRepository<EducationBoard, Guid> educationBoardRepository,
        IEducationMediumRepository educationMediumRepository,
        IEducationMediumStageRepository educationMediumStageRepository,
        IRepository<Standard, Guid> standardRepository,
        IRepository<Student, Guid> studentRepository,
        IRepository<FeeCategory, Guid> feeCategoryRepository,
        IRepository<FeeHead, Guid> feeHeadRepository,
        IRepository<ReceiptBook, Guid> receiptBookRepository,
        IRepository<FeeStructureMetadata, Guid> feeStructureMetadataRepository,
        IRepository<FeeStructure, Guid> feeStructureRepository,
        IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
        IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
        IDueFeeRepository dueFeeRepository,
        IRepository<FeeRecordMetadata, Guid> feeRecordMetadataRepository,
        IRepository<FeeCollectionRecord, Guid> feeCollectionRecordRepository,
        IRepository<PaymentMethodRecord, Guid> paymentMethodRecordRepository,
        ILogger<FeeCollectionSeederContributor> logger)
        : base(currentTenant, guidGenerator, unitOfWork)
    {
        _academicYearRepository          = academicYearRepository;
        _educationBoardRepository        = educationBoardRepository;
        _educationMediumRepository       = educationMediumRepository;
        _educationMediumStageRepository  = educationMediumStageRepository;
        _standardRepository              = standardRepository;
        _studentRepository               = studentRepository;
        _feeCategoryRepository           = feeCategoryRepository;
        _feeHeadRepository               = feeHeadRepository;
        _receiptBookRepository           = receiptBookRepository;
        _feeStructureMetadataRepository  = feeStructureMetadataRepository;
        _feeStructureRepository          = feeStructureRepository;
        _feeStructureDetailRepository    = feeStructureDetailRepository;
        _studentFeeStructureRepository   = studentFeeStructureRepository;
        _dueFeeRepository                = dueFeeRepository;
        _feeRecordMetadataRepository     = feeRecordMetadataRepository;
        _feeCollectionRecordRepository   = feeCollectionRecordRepository;
        _paymentMethodRecordRepository   = paymentMethodRecordRepository;
        _logger                          = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────

    public override async Task SeedAsync(DataSeedContext context)
    {
        // Idempotency guard — skip if any fee collection receipts already exist
        var existingCount = await _feeRecordMetadataRepository.GetCountAsync();
        if (existingCount > 0)
        {
            _logger.LogInformation(
                "[FeeCollectionSeeder] Fee collection data already present ({Count} receipts). Skipping. TenantId={TenantId}",
                existingCount, CurrentTenant.Id);
            return;
        }

        _logger.LogInformation("[FeeCollectionSeeder] Starting seed. TenantId={TenantId}", CurrentTenant.Id);

        // ── 1. Academic year ─────────────────────────────────────────────────

        var academicYear = await ResolveAcademicYearAsync();
        if (academicYear is null) return;

        // ── 2. Board / Medium / Stage / Standards ─────────────────────────────

        var setup = await ResolveAcademicSetupAsync(academicYear.Id);
        if (setup is null) return;

        var (board, medium, stage, standards) = setup.Value;

        // ── 3. Fee category + heads ───────────────────────────────────────────

        var category   = await GetOrCreateFeeCategoryAsync(academicYear.Id);
        var tuitionHead = await GetOrCreateFeeHeadAsync(academicYear.Id, category.Id, TuitionFeeHeadName, "Monthly tuition charge");
        var devHead     = await GetOrCreateFeeHeadAsync(academicYear.Id, category.Id, DevFeeHeadName,     "One-time annual development fee");

        await CurrentUnitOfWork.SaveChangesAsync();

        // ── 4. Receipt book ───────────────────────────────────────────────────

        var receiptBook = await GetOrCreateReceiptBookAsync(academicYear.Id, board.Id, medium.Id, stage.Id);
        await CurrentUnitOfWork.SaveChangesAsync();

        // ── 5. Fee structure metadata + per-standard structures ───────────────

        var metadata    = await GetOrCreateFeeStructureMetadataAsync(academicYear.Id, board.Id, medium.Id, receiptBook.Id);
        await CurrentUnitOfWork.SaveChangesAsync();

        // Map standardId → FeeStructure for assignment later
        var feeStructureByStandard = await BuildFeeStructuresAsync(
            metadata.Id, standards, tuitionHead.Id, devHead.Id, academicYear);
        await CurrentUnitOfWork.SaveChangesAsync();

        // ── 6. Enrolled students ──────────────────────────────────────────────

        var enrolledStudents = await _studentRepository.GetListAsync(s =>
            s.AcademicYearId   == academicYear.Id
         && s.AcademicBoardId  == board.Id
         && s.AcademicMediumId == medium.Id
         && s.Status           == StudentLifeCycleStatusEnum.Enrolled);

        if (enrolledStudents.Count == 0)
        {
            _logger.LogWarning("[FeeCollectionSeeder] No enrolled students found. Aborting.");
            return;
        }

        _logger.LogInformation("[FeeCollectionSeeder] Found {Count} enrolled students.", enrolledStudents.Count);

        // ── 7. StudentFeeStructure + DueFees ─────────────────────────────────

        var monthRange = ComputeMonthRange(academicYear);
        var monthlyAmount = AnnualTuitionFee / monthRange.Count;

        var studentsWithNewDueFees = new List<Student>();
        var enrolledStudentIds = enrolledStudents.Select(x => x.Id).ToList();
        var assignedStudentIds = (await _studentFeeStructureRepository.GetListAsync(
                sfs => enrolledStudentIds.Contains(sfs.StudentId)))
            .Select(x => x.StudentId)
            .ToHashSet();
        var studentsWithExistingDueFees = (await _dueFeeRepository.GetListAsync(
                dueFee => enrolledStudentIds.Contains(dueFee.StudentId)))
            .Select(x => x.StudentId)
            .ToHashSet();
        var studentFeeStructuresToInsert = new List<StudentFeeStructure>();
        var dueFeesToInsert = new List<DueFee>();

        foreach (var student in enrolledStudents)
        {
            if (student.StandardId is null) continue;
            if (!feeStructureByStandard.TryGetValue(student.StandardId.Value, out var feeStructure))
                continue;   // student's standard has no seed fee structure

            // Create StudentFeeStructure assignment (idempotent check inside)
            if (!assignedStudentIds.Contains(student.Id))
            {
                studentFeeStructuresToInsert.Add(
                    new StudentFeeStructure(
                        id:                     GuidGenerator.Create(),
                        tenantId:               CurrentTenant.Id,
                        studentId:              student.Id,
                        feeStructureStandardId: feeStructure.Id));
                assignedStudentIds.Add(student.Id);
            }

            if (student.FeeStructureId != feeStructure.Id)
            {
                student.AssignFeeStructure(feeStructure.Id);
                await _studentRepository.UpdateAsync(student, autoSave: false);
            }

            // Skip DueFee generation if any already exist (e.g. from FeeRefundSeeder)
            if (studentsWithExistingDueFees.Contains(student.Id))
                continue;

            // Monthly tuition DueFees
            var dueFees = monthRange.Select(mr => new DueFee(
                id:        GuidGenerator.Create(),
                tenantId:  CurrentTenant.Id,
                studentId: student.Id,
                feeHeadId: tuitionHead.Id,
                month:     mr.Month,
                year:      mr.Year,
                isOneTime: false,
                amount:    monthlyAmount)).ToList();

            // One-time development fee (due in first month of academic year)
            dueFees.Add(new DueFee(
                id:        GuidGenerator.Create(),
                tenantId:  CurrentTenant.Id,
                studentId: student.Id,
                feeHeadId: devHead.Id,
                month:     monthRange[0].Month,
                year:      monthRange[0].Year,
                isOneTime: true,
                amount:    DevFeeAmount));

            dueFeesToInsert.AddRange(dueFees);
            studentsWithExistingDueFees.Add(student.Id);
            studentsWithNewDueFees.Add(student);
        }

        if (studentFeeStructuresToInsert.Count > 0)
        {
            await _studentFeeStructureRepository.InsertManyAsync(studentFeeStructuresToInsert, autoSave: false);
        }

        if (dueFeesToInsert.Count > 0)
        {
            await _dueFeeRepository.InsertManyAsync(dueFeesToInsert, autoSave: false);
        }

        await CurrentUnitOfWork.SaveChangesAsync();

        // ── 8. Sample payment receipts ────────────────────────────────────────

        if (studentsWithNewDueFees.Count >= 1)
            await SeedPaymentRecordsAsync(
                studentsWithNewDueFees, tuitionHead.Id, academicYear.Id,
                monthlyAmount, monthRange[0].Month, monthRange[0].Year,
                monthRange[1].Month, monthRange[1].Year);

        await CurrentUnitOfWork.SaveChangesAsync();

        _logger.LogInformation("[FeeCollectionSeeder] Seed completed. TenantId={TenantId}", CurrentTenant.Id);
    }

    // ─── Prerequisites ────────────────────────────────────────────────────────

    private async Task<AcademicYear?> ResolveAcademicYearAsync()
    {
        var year = DateTime.UtcNow.Year;
        var ay = await _academicYearRepository.FirstOrDefaultAsync(y => y.Name.StartsWith(year.ToString()))
              ?? await _academicYearRepository.FirstOrDefaultAsync();

        if (ay is null)
            _logger.LogWarning("[FeeCollectionSeeder] No academic year found — aborting.");

        return ay;
    }

    private async Task<(EducationBoard Board, EducationMedium Medium, EducationMediumStage Stage, List<Standard> Standards)?> ResolveAcademicSetupAsync(Guid academicYearId)
    {
        var board = await _educationBoardRepository.FirstOrDefaultAsync(b => b.AcademicYearId == academicYearId);
        if (board is null)
        {
            _logger.LogWarning("[FeeCollectionSeeder] No education board found — aborting.");
            return null;
        }

        var medium = await _educationMediumRepository.FirstOrDefaultAsync(m => m.EducationBoardId == board.Id);
        if (medium is null)
        {
            _logger.LogWarning("[FeeCollectionSeeder] No education medium found — aborting.");
            return null;
        }

        var stage = await _educationMediumStageRepository.FirstOrDefaultAsync(s => s.EducationMediumId == medium.Id);
        if (stage is null)
        {
            _logger.LogWarning("[FeeCollectionSeeder] No education medium stage found — aborting.");
            return null;
        }

        var allStandards = await _standardRepository.GetListAsync(s => s.EducationMediumStageId == stage.Id);
        var standards    = allStandards.Take(MaxStructuredStandards).ToList();

        if (standards.Count == 0)
        {
            _logger.LogWarning("[FeeCollectionSeeder] No standards found — aborting.");
            return null;
        }

        _logger.LogDebug(
            "[FeeCollectionSeeder] Setup resolved: Board={Board}, Medium={Medium}, Stage={Stage}, Standards={Count}",
            board.Id, medium.Id, stage.Id, standards.Count);

        return (board, medium, stage, standards);
    }

    // ─── Fee settings ─────────────────────────────────────────────────────────

    private async Task<FeeCategory> GetOrCreateFeeCategoryAsync(Guid academicYearId)
    {
        var existing = (await _feeCategoryRepository.GetListAsync(c => c.Name == FeeCategoryName))
                       .FirstOrDefault();
        if (existing is not null) return existing;

        var created = await _feeCategoryRepository.InsertAsync(
            new FeeCategory(
                id:             GuidGenerator.Create(),
                tenantId:       CurrentTenant.Id,
                academicYearId: academicYearId,
                name:           FeeCategoryName,
                description:    "Auto-created by fee-collection seed contributor."),
            autoSave: false);

        _logger.LogDebug("[FeeCollectionSeeder] Created FeeCategory: {Name}", created.Name);
        return created;
    }

    private async Task<FeeHead> GetOrCreateFeeHeadAsync(
        Guid academicYearId, Guid categoryId, string name, string description)
    {
        var existing = (await _feeHeadRepository.GetListAsync(h => h.Name == name)).FirstOrDefault();
        if (existing is not null) return existing;

        var created = await _feeHeadRepository.InsertAsync(
            new FeeHead(
                id:             GuidGenerator.Create(),
                tenantId:       CurrentTenant.Id,
                academicYearId: academicYearId,
                feeCategoryId:  categoryId,
                name:           name,
                description:    description),
            autoSave: false);

        _logger.LogDebug("[FeeCollectionSeeder] Created FeeHead: {Name}", created.Name);
        return created;
    }

    // ─── Receipt book ─────────────────────────────────────────────────────────

    private async Task<ReceiptBook> GetOrCreateReceiptBookAsync(
        Guid academicYearId, Guid boardId, Guid mediumId, Guid mediumStageId)
    {
        const string name = "Seed – Main Receipt Book";

        var existing = (await _receiptBookRepository.GetListAsync(rb => rb.Name == name)).FirstOrDefault();
        if (existing is not null) return existing;

        var created = await _receiptBookRepository.InsertAsync(
            new ReceiptBook(
                id:             GuidGenerator.Create(),
                tenantId:       CurrentTenant.Id,
                academicYearId: academicYearId,
                boardId:        boardId,
                mediumId:       mediumId,
                mediumStageId:  mediumStageId,
                name:           name,
                prefix:         "REC",
                suffix:         string.Empty,
                startingNumber: 1,
                endingNumber:   99_999,
                description:    "Auto-created by fee-collection seed contributor."),
            autoSave: false);

        _logger.LogDebug("[FeeCollectionSeeder] Created ReceiptBook: {Name}", created.Name);
        return created;
    }

    // ─── Fee structure ────────────────────────────────────────────────────────

    private async Task<FeeStructureMetadata> GetOrCreateFeeStructureMetadataAsync(
        Guid academicYearId, Guid boardId, Guid mediumId, Guid receiptBookId)
    {
        const string name = "Seed – Main Fee Structure";

        var existing = (await _feeStructureMetadataRepository.GetListAsync(m => m.Name == name)).FirstOrDefault();
        if (existing is not null) return existing;

        var created = await _feeStructureMetadataRepository.InsertAsync(
            new FeeStructureMetadata(
                id:            GuidGenerator.Create(),
                tenantId:      CurrentTenant.Id,
                academicYearId: academicYearId,
                boardId:       boardId,
                mediumId:      mediumId,
                receiptBookId: receiptBookId,
                name:          name),
            autoSave: false);

        _logger.LogDebug("[FeeCollectionSeeder] Created FeeStructureMetadata: {Name}", created.Name);
        return created;
    }

    /// <summary>
    /// Creates one <see cref="FeeStructure"/> per standard (up to <see cref="MaxStructuredStandards"/>).
    /// Returns a dictionary mapping StandardId → FeeStructure.
    /// </summary>
    private async Task<Dictionary<Guid, FeeStructure>> BuildFeeStructuresAsync(
        Guid metadataId,
        List<Standard> standards,
        Guid tuitionFeeHeadId,
        Guid devFeeHeadId,
        AcademicYear academicYear)
    {
        var result = new Dictionary<Guid, FeeStructure>();

        // Load any structures already associated with this metadata to stay idempotent
        var existing = await _feeStructureRepository.GetListAsync(fs => fs.FeeStructureMetadataId == metadataId);
        foreach (var existingFs in existing)
            result[existingFs.StandardId] = existingFs;

        var monthRange    = ComputeMonthRange(academicYear);
        var monthlyAmount = AnnualTuitionFee / monthRange.Count;

        foreach (var standard in standards)
        {
            if (result.TryGetValue(standard.Id, out var existingStructure))
            {
                await EnsureFeeStructureDetailsAsync(
                    existingStructure, tuitionFeeHeadId, devFeeHeadId, academicYear);
                continue;
            }

            var structure = new FeeStructure(
                id:                    GuidGenerator.Create(),
                tenantId:              CurrentTenant.Id,
                feeStructureMetadataId: metadataId,
                standardId:            standard.Id,
                name:                  $"Seed – {standard.Name} Fee Structure");

            structure.AddDetail(new FeeStructureDetail(
                id: GuidGenerator.Create(),
                tenantId: CurrentTenant.Id,
                feeStructureId: structure.Id,
                feeHeadId: tuitionFeeHeadId,
                amount: monthlyAmount,
                isOneTime: false,
                isOptional: false,
                payableMonth: null,
                payableYear: null));

            // One-time development fee (due in first month)
            structure.AddDetail(new FeeStructureDetail(
                id:              GuidGenerator.Create(),
                tenantId:        CurrentTenant.Id,
                feeStructureId:  structure.Id,
                feeHeadId:       devFeeHeadId,
                amount:          DevFeeAmount,
                isOneTime:       true,
                isOptional:      false,
                payableMonth:    monthRange[0].Month,
                payableYear:     monthRange[0].Year));

            await _feeStructureRepository.InsertAsync(structure, autoSave: false);
            foreach (var detail in structure.FeeStructureDetails)
                await _feeStructureDetailRepository.InsertAsync(detail, autoSave: false);

            result[standard.Id] = structure;

            _logger.LogDebug("[FeeCollectionSeeder] Created FeeStructure: {Name}", structure.Name);
        }

        return result;
    }

    /// <summary>
    /// Ensures fee-structure detail rows exist (repair path for structures created without persisted children).
    /// </summary>
    private async Task EnsureFeeStructureDetailsAsync(
        FeeStructure structure,
        Guid tuitionFeeHeadId,
        Guid devFeeHeadId,
        AcademicYear academicYear)
    {
        var existingCount = await _feeStructureDetailRepository.CountAsync(d => d.FeeStructureId == structure.Id);
        if (existingCount > 0)
            return;

        var monthRange = ComputeMonthRange(academicYear);
        var monthlyAmount = AnnualTuitionFee / monthRange.Count;

        await _feeStructureDetailRepository.InsertAsync(new FeeStructureDetail(
            id: GuidGenerator.Create(),
            tenantId: CurrentTenant.Id,
            feeStructureId: structure.Id,
            feeHeadId: tuitionFeeHeadId,
            amount: monthlyAmount,
            isOneTime: false,
            isOptional: false,
            payableMonth: null,
            payableYear: null), autoSave: false);

        await _feeStructureDetailRepository.InsertAsync(new FeeStructureDetail(
            id: GuidGenerator.Create(),
            tenantId: CurrentTenant.Id,
            feeStructureId: structure.Id,
            feeHeadId: devFeeHeadId,
            amount: DevFeeAmount,
            isOneTime: true,
            isOptional: false,
            payableMonth: monthRange[0].Month,
            payableYear: monthRange[0].Year), autoSave: false);
    }

    // ─── Sample payment receipts ──────────────────────────────────────────────

    /// <summary>
    /// Creates a small set of realistic payment receipts:
    /// <list type="bullet">
    ///   <item>Student 0 — June fully paid (Cash)</item>
    ///   <item>Student 0 — July fully paid (UPI)</item>
    ///   <item>Student 1 — June partially paid (Cash, ₹500)</item>
    /// </list>
    /// Also updates the corresponding <see cref="DueFee"/> records via
    /// <see cref="DueFee.ApplyPayment"/> to keep ledger consistent.
    /// </summary>
    private async Task SeedPaymentRecordsAsync(
        List<Student> students,
        Guid tuitionFeeHeadId,
        Guid academicYearId,
        double monthlyAmount,
        MonthEnum month0, int year0,
        MonthEnum month1, int year1)
    {
        var now = DateTimeOffset.UtcNow;

        // ── Receipt 1: Student 0, Month 0 (June) — Cash, fully paid ──────────
        var student0   = students[0];
        var dueFeeJun0 = await _dueFeeRepository.FirstOrDefaultAsync(
            d => d.StudentId == student0.Id && d.FeeHeadId == tuitionFeeHeadId
              && d.Month == month0 && d.Year == year0);

        if (dueFeeJun0 is not null)
        {
            await CreateReceiptAsync(
                receiptNumber:  "REC-SEED-000001",
                studentId:      student0.Id,
                academicYearId: academicYearId,
                feeHeadId:      tuitionFeeHeadId,
                amount:         monthlyAmount,
                received:       monthlyAmount,
                paymentDate:    now.AddMonths(-2),
                paymentMethod:  PaymentMethodEnum.Cash,
                paymentDetails: null,
                dueFee:         dueFeeJun0);

            _logger.LogDebug("[FeeCollectionSeeder] Inserted receipt REC-SEED-000001 (student 0, month 0, Cash)");
        }

        // ── Receipt 2: Student 0, Month 1 (July) — UPI, fully paid ───────────
        var dueFeeJul0 = await _dueFeeRepository.FirstOrDefaultAsync(
            d => d.StudentId == student0.Id && d.FeeHeadId == tuitionFeeHeadId
              && d.Month == month1 && d.Year == year1);

        if (dueFeeJul0 is not null)
        {
            await CreateReceiptAsync(
                receiptNumber:  "REC-SEED-000002",
                studentId:      student0.Id,
                academicYearId: academicYearId,
                feeHeadId:      tuitionFeeHeadId,
                amount:         monthlyAmount,
                received:       monthlyAmount,
                paymentDate:    now.AddMonths(-1),
                paymentMethod:  PaymentMethodEnum.UPI,
                paymentDetails: new UpiDetails(
                    upiTransactionId: "TXN-SEED-0002",
                    upiId:            "school.fees@okicici",
                    appName:          "GPay"),
                dueFee:         dueFeeJul0);

            _logger.LogDebug("[FeeCollectionSeeder] Inserted receipt REC-SEED-000002 (student 0, month 1, UPI)");
        }

        // ── Receipt 3: Student 1 (if available), Month 0 — partial Cash ───────
        if (students.Count < 2) return;

        var student1      = students[1];
        var dueFeeJun1 = await _dueFeeRepository.FirstOrDefaultAsync(
            d => d.StudentId == student1.Id && d.FeeHeadId == tuitionFeeHeadId
              && d.Month == month0 && d.Year == year0);

        if (dueFeeJun1 is not null)
        {
            const double partialAmount = 500d;
            await CreateReceiptAsync(
                receiptNumber:  "REC-SEED-000003",
                studentId:      student1.Id,
                academicYearId: academicYearId,
                feeHeadId:      tuitionFeeHeadId,
                amount:         monthlyAmount,
                received:       partialAmount,
                paymentDate:    now.AddDays(-15),
                paymentMethod:  PaymentMethodEnum.Cash,
                paymentDetails: null,
                dueFee:         dueFeeJun1);

            _logger.LogDebug("[FeeCollectionSeeder] Inserted receipt REC-SEED-000003 (student 1, month 0, Cash partial)");
        }
    }

    private async Task CreateReceiptAsync(
        string receiptNumber,
        Guid studentId,
        Guid academicYearId,
        Guid feeHeadId,
        double amount,
        double received,
        DateTimeOffset paymentDate,
        PaymentMethodEnum paymentMethod,
        PaymentDetails? paymentDetails,
        DueFee dueFee)
    {
        var status = received >= amount ? FeeCollectionStatus.Paid : FeeCollectionStatus.Pending;

        var metadata = new FeeRecordMetadata(
            GuidGenerator.Create(), CurrentTenant.Id, studentId, academicYearId,
            received, receiptNumber, status);
        await _feeRecordMetadataRepository.InsertAsync(metadata, autoSave: false);

        var collectionRecord = new FeeCollectionRecord(GuidGenerator.Create())
        {
            TenantId              = CurrentTenant.Id,
            StudentId             = studentId,
            FeeHeadId             = feeHeadId,
            Amount                = received,
            PaymentDate           = paymentDate,
            FeeRecordMetadataId   = metadata.Id,
        };
        await _feeCollectionRecordRepository.InsertAsync(collectionRecord, autoSave: false);

        var paymentRecord = PaymentMethodRecord.Create(
            studentId:           studentId,
            feeRecordMetadataId: metadata.Id,
            amount:              (decimal)received,
            paymentDate:         paymentDate,
            paymentMethod:       paymentMethod,
            paymentDetails:      paymentDetails,
            tenantId:            CurrentTenant.Id);
        await _paymentMethodRecordRepository.InsertAsync(paymentRecord, autoSave: false);

        // Reflect the payment in the DueFee ledger
        dueFee.ApplyPayment(received);
        await _dueFeeRepository.UpdateAsync(dueFee, autoSave: false);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns every (MonthEnum, Year) pair from the academic year start month
    /// through the academic year end month, inclusive.
    /// </summary>
    private static List<(MonthEnum Month, int Year)> ComputeMonthRange(AcademicYear academicYear)
    {
        var result  = new List<(MonthEnum, int)>();
        var current = new DateTime(academicYear.StartDate.Year, academicYear.StartDate.Month, 1);
        var end     = new DateTime(academicYear.EndDate.Year,   academicYear.EndDate.Month,   1);

        while (current <= end)
        {
            result.Add(((MonthEnum)(current.Month - 1), current.Year));
            current = current.AddMonths(1);
        }

        return result;
    }
}
