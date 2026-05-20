using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Collections;
using ShikshaMitra.Finance.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Onboarding.AcademicYears;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Seeds realistic fee-refund test data so developers can immediately exercise the
/// refund list, eligibility, create and approve flows without manually setting up
/// fee heads, due fees, and refund requests through the UI.
///
/// Seeding strategy
/// ────────────────
/// • Idempotent: exits early when any <see cref="FeeRefundRequest"/> already exists
///   for the current tenant.
/// • Depends on <see cref="StudentSeederContributor"/> (students must be present).
/// • Depends on <see cref="AcademicSetupSeederContributor"/> (academic year must exist).
/// • Inserts the minimum data required:
///     1. A "Seed – Tuition" fee category + "Seed – Monthly Tuition Fee" fee head
///        (name-guarded so it is created only once).
///     2. Four fully-paid monthly <see cref="DueFee"/> rows for the first seeded student
///        (June, July, August, September of the current calendar year).
///     3. Three refund requests in different lifecycle states for the UI to display:
///        – REF-SEED-000001  Pending        (June + July lines, ₹3 500)
///        – REF-SEED-000002  Under Review   (August line,       ₹1 500)
///        – REF-SEED-000003  Approved       (September line,    ₹1 800)
///          → also calls <see cref="DueFee.ApplyRefund"/> so the ledger is consistent.
/// </summary>
public class FeeRefundSeederContributor : BaseSeederContributor
{
    // ── Seed constants ────────────────────────────────────────────────────────

    private const string FeeCategoryName = "Seed – Tuition";
    private const string FeeHeadName     = "Seed – Monthly Tuition Fee";

    // Monthly obligation amounts (₹)
    private static readonly (MonthEnum Month, double Amount)[] SeedMonths =
    [
        (MonthEnum.June,      1_500d),
        (MonthEnum.July,      2_000d),
        (MonthEnum.August,    1_500d),
        (MonthEnum.September, 1_800d),
    ];

    // ── Dependencies ─────────────────────────────────────────────────────────

    private readonly IAcademicYearRepository                _academicYearRepository;
    private readonly IRepository<Student, Guid>             _studentRepository;
    private readonly IRepository<FeeCategory, Guid>         _feeCategoryRepository;
    private readonly IRepository<FeeHead, Guid>             _feeHeadRepository;
    private readonly IDueFeeRepository                      _dueFeeRepository;
    private readonly IFeeRefundRepository                   _feeRefundRepository;
    private readonly ILogger<FeeRefundSeederContributor>    _logger;

    public FeeRefundSeederContributor(
        ICurrentTenant currentTenant,
        IGuidGenerator guidGenerator,
        IUnitOfWork unitOfWork,
        IAcademicYearRepository academicYearRepository,
        IRepository<Student, Guid> studentRepository,
        IRepository<FeeCategory, Guid> feeCategoryRepository,
        IRepository<FeeHead, Guid> feeHeadRepository,
        IDueFeeRepository dueFeeRepository,
        IFeeRefundRepository feeRefundRepository,
        ILogger<FeeRefundSeederContributor> logger)
        : base(currentTenant, guidGenerator, unitOfWork)
    {
        _academicYearRepository = academicYearRepository;
        _studentRepository      = studentRepository;
        _feeCategoryRepository  = feeCategoryRepository;
        _feeHeadRepository      = feeHeadRepository;
        _dueFeeRepository       = dueFeeRepository;
        _feeRefundRepository    = feeRefundRepository;
        _logger                 = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────

    public override async Task SeedAsync(DataSeedContext context)
    {
        // Idempotency guard — skip if any refund requests already exist
        var existingCount = await _feeRefundRepository.GetCountAsync();
        if (existingCount > 0)
        {
            _logger.LogInformation(
                "[FeeRefundSeeder] Refund data already present ({Count} records). Skipping. TenantId={TenantId}",
                existingCount, CurrentTenant.Id);
            return;
        }

        _logger.LogInformation("[FeeRefundSeeder] Starting seed. TenantId={TenantId}", CurrentTenant.Id);

        // ── 1. Prerequisites ─────────────────────────────────────────────────

        var academicYear = await ResolveAcademicYearAsync();
        if (academicYear is null) return;

        var student = await ResolveStudentAsync();
        if (student is null) return;

        // ── 2. Fee category & fee head ────────────────────────────────────────

        var category = await GetOrCreateFeeCategoryAsync(academicYear.Id);
        var feeHead  = await GetOrCreateFeeHeadAsync(academicYear.Id, category.Id);

        // Flush category/head so FK constraints are satisfied before DueFees
        await CurrentUnitOfWork.SaveChangesAsync();

        // ── 3. Monthly due fees (fully paid) ──────────────────────────────────

        var calendarYear = DateTime.UtcNow.Year;
        var dueFeeMap = await SeedDueFeesAsync(student.Id, feeHead.Id, calendarYear);

        await CurrentUnitOfWork.SaveChangesAsync();

        // ── 4. Refund requests in various lifecycle states ────────────────────

        await SeedRefundRequestsAsync(student.Id, academicYear.Id, dueFeeMap);

        await CurrentUnitOfWork.SaveChangesAsync();

        _logger.LogInformation("[FeeRefundSeeder] Seed completed. TenantId={TenantId}", CurrentTenant.Id);
    }

    // ─── Prerequisites ───────────────────────────────────────────────────────

    private async Task<AcademicYear?> ResolveAcademicYearAsync()
    {
        var year = DateTime.UtcNow.Year;
        var academicYear =
            await _academicYearRepository.FirstOrDefaultAsync(y => y.Name.StartsWith(year.ToString()))
            ?? await _academicYearRepository.FirstOrDefaultAsync();

        if (academicYear is null)
        {
            _logger.LogWarning("[FeeRefundSeeder] No academic year found — aborting.");
            return null;
        }

        _logger.LogDebug("[FeeRefundSeeder] Using AcademicYear: {Id} – {Name}", academicYear.Id, academicYear.Name);
        return academicYear;
    }

    private async Task<Student?> ResolveStudentAsync()
    {
        var student = await _studentRepository.FirstOrDefaultAsync();
        if (student is null)
        {
            _logger.LogWarning("[FeeRefundSeeder] No students found — run StudentSeeder first. Aborting.");
            return null;
        }

        _logger.LogDebug("[FeeRefundSeeder] Using Student: {Id} (GR {GrNo})", student.Id, student.GrNo);
        return student;
    }

    // ─── Fee category & head ─────────────────────────────────────────────────

    private async Task<FeeCategory> GetOrCreateFeeCategoryAsync(Guid academicYearId)
    {
        var existing = (await _feeCategoryRepository
            .GetListAsync(c => c.Name == FeeCategoryName))
            .FirstOrDefault();

        if (existing is not null) return existing;

        var created = await _feeCategoryRepository.InsertAsync(
            new FeeCategory(
                id:            GuidGenerator.Create(),
                tenantId:      CurrentTenant.Id,
                academicYearId: academicYearId,
                name:          FeeCategoryName,
                description:   "Auto-created by seed data contributor for fee-refund testing."),
            autoSave: false);

        _logger.LogDebug("[FeeRefundSeeder] Created FeeCategory: {Id} – {Name}", created.Id, created.Name);
        return created;
    }

    private async Task<FeeHead> GetOrCreateFeeHeadAsync(Guid academicYearId, Guid categoryId)
    {
        var existing = (await _feeHeadRepository
            .GetListAsync(h => h.Name == FeeHeadName))
            .FirstOrDefault();

        if (existing is not null) return existing;

        var created = await _feeHeadRepository.InsertAsync(
            new FeeHead(
                id:             GuidGenerator.Create(),
                tenantId:       CurrentTenant.Id,
                academicYearId: academicYearId,
                feeCategoryId:  categoryId,
                name:           FeeHeadName,
                description:    "Auto-created by seed data contributor for fee-refund testing."),
            autoSave: false);

        _logger.LogDebug("[FeeRefundSeeder] Created FeeHead: {Id} – {Name}", created.Id, created.Name);
        return created;
    }

    // ─── DueFee rows ─────────────────────────────────────────────────────────

    private async Task<Dictionary<MonthEnum, DueFee>> SeedDueFeesAsync(
        Guid studentId, Guid feeHeadId, int calendarYear)
    {
        // Load any already-existing rows for this student+head to stay idempotent
        var existingDueFees = await _dueFeeRepository
            .GetListAsync(df => df.StudentId == studentId && df.FeeHeadId == feeHeadId);

        var existingByMonth = existingDueFees
            .ToDictionary(df => df.Month);

        var result = new Dictionary<MonthEnum, DueFee>();
        var toInsert = new List<DueFee>();

        foreach (var (month, amount) in SeedMonths)
        {
            if (existingByMonth.TryGetValue(month, out var existing))
            {
                result[month] = existing;
                continue;
            }

            var dueFee = new DueFee(
                id:        GuidGenerator.Create(),
                tenantId:  CurrentTenant.Id,
                studentId: studentId,
                feeHeadId: feeHeadId,
                month:     month,
                year:      calendarYear,
                isOneTime: false,
                amount:    amount);

            // Simulate that the student has fully paid this month's fee
            dueFee.ApplyPayment(amount);

            toInsert.Add(dueFee);
            result[month] = dueFee;

            _logger.LogDebug(
                "[FeeRefundSeeder] Prepared DueFee: {Month}/{Year} – ₹{Amount} (fully paid)",
                month, calendarYear, amount);
        }

        if (toInsert.Count > 0)
            await _dueFeeRepository.InsertManyAsync(toInsert, autoSave: false);

        return result;
    }

    // ─── Refund requests ─────────────────────────────────────────────────────

    private async Task SeedRefundRequestsAsync(
        Guid studentId, Guid academicYearId,
        Dictionary<MonthEnum, DueFee> dueFeeMap)
    {
        // ── Request 1: Pending — June + July ─────────────────────────────────
        var pending = CreateRequest(
            voucherNumber: "REF-SEED-000001",
            studentId:     studentId,
            academicYearId: academicYearId,
            reason:        "Student transferred to another school",
            refundMethod:  "Cash");

        pending.AddLine(MakeLine(pending.Id, dueFeeMap[MonthEnum.June],      1_500d));
        pending.AddLine(MakeLine(pending.Id, dueFeeMap[MonthEnum.July],      2_000d));
        // Status stays Pending (default)

        await _feeRefundRepository.InsertAsync(pending, autoSave: false);

        _logger.LogDebug("[FeeRefundSeeder] Inserted Pending refund: {Voucher}", pending.VoucherNumber);

        // ── Request 2: Under Review — August ─────────────────────────────────
        var underReview = CreateRequest(
            voucherNumber: "REF-SEED-000002",
            studentId:     studentId,
            academicYearId: academicYearId,
            reason:        "Fee charged in error for absent month",
            refundMethod:  "Bank Transfer");

        underReview.AddLine(MakeLine(underReview.Id, dueFeeMap[MonthEnum.August], 1_500d));
        underReview.MarkUnderReview();

        await _feeRefundRepository.InsertAsync(underReview, autoSave: false);

        _logger.LogDebug("[FeeRefundSeeder] Inserted UnderReview refund: {Voucher}", underReview.VoucherNumber);

        // ── Request 3: Approved — September ──────────────────────────────────
        var approved = CreateRequest(
            voucherNumber: "REF-SEED-000003",
            studentId:     studentId,
            academicYearId: academicYearId,
            reason:        "Excess amount collected during admission",
            refundMethod:  "UPI");

        approved.AddLine(MakeLine(approved.Id, dueFeeMap[MonthEnum.September], 1_800d));

        // Approve: transition status and stamp processor (Guid.Empty = system/seeder)
        approved.Approve(Guid.Empty, notes: "Verified by finance admin. Auto-approved during seed.");

        // Reflect the approval on the DueFee ledger so RefundableBalance is consistent
        dueFeeMap[MonthEnum.September].ApplyRefund(1_800d);
        await _dueFeeRepository.UpdateAsync(dueFeeMap[MonthEnum.September], autoSave: false);

        await _feeRefundRepository.InsertAsync(approved, autoSave: false);

        _logger.LogDebug("[FeeRefundSeeder] Inserted Approved refund: {Voucher}", approved.VoucherNumber);
    }

    // ─── Factories ───────────────────────────────────────────────────────────

    private FeeRefundRequest CreateRequest(
        string voucherNumber,
        Guid studentId,
        Guid academicYearId,
        string? reason,
        string? refundMethod)
        => new(
            id:             GuidGenerator.Create(),
            tenantId:       CurrentTenant.Id,
            studentId:      studentId,
            academicYearId: academicYearId,
            voucherNumber:  voucherNumber,
            totalAmount:    0d,   // updated by AddLine / not enforced by domain for seeded data
            refundMethod:   refundMethod,
            reason:         reason);

    private FeeRefundLine MakeLine(Guid requestId, DueFee dueFee, double amount)
        => new(
            id:                 GuidGenerator.Create(),
            feeRefundRequestId: requestId,
            dueFeeId:           dueFee.Id,
            amount:             amount);
}
