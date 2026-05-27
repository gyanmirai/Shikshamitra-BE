using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

/// <summary>
/// Integration tests for <see cref="FeeCollectionAppServiceV2.CollectFeeAsync"/> —
/// verifies that the correct receipt kind is issued for each payment-method scenario.
///
/// Receipt prefix → kind mapping:
///   FIN-* → FinalFee          (Cash, or CashierDecided with BankReceiptConfirmed=true)
///   KFE-* → ProvisionalFee    (Cheque/DD, or CashierDecided with BankReceiptConfirmed=false)
///   KCH-* → ProvisionalCharge (Charges, always provisional regardless of payment method)
/// </summary>
public class FeeCollectionAppServiceV2_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    private readonly IFeeCollectionAppServiceV2 _service;
    private readonly IRepository<AcademicYear, Guid> _academicYearRepo;
    private readonly IRepository<FeeCategory, Guid> _feeCategoryRepo;
    private readonly IRepository<FeeHead, Guid> _feeHeadRepo;
    private readonly IRepository<Student, Guid> _studentRepo;
    private readonly IRepository<EducationBoard, Guid> _boardRepo;
    private readonly IRepository<EducationMedium, Guid> _mediumRepo;
    private readonly IRepository<EducationMediumStage, Guid> _stageRepo;
    private readonly ICurrentTenant _currentTenant;

    public FeeCollectionAppServiceV2_Tests()
    {
        _service            = GetRequiredService<IFeeCollectionAppServiceV2>();
        _academicYearRepo   = GetRequiredService<IRepository<AcademicYear, Guid>>();
        _feeCategoryRepo    = GetRequiredService<IRepository<FeeCategory, Guid>>();
        _feeHeadRepo        = GetRequiredService<IRepository<FeeHead, Guid>>();
        _studentRepo        = GetRequiredService<IRepository<Student, Guid>>();
        _boardRepo          = GetRequiredService<IRepository<EducationBoard, Guid>>();
        _mediumRepo         = GetRequiredService<IRepository<EducationMedium, Guid>>();
        _stageRepo          = GetRequiredService<IRepository<EducationMediumStage, Guid>>();
        _currentTenant      = GetRequiredService<ICurrentTenant>();
    }

    // ── Scenario: Cash payment → FinalFee receipt (FIN-*) ────────────────────

    [Fact]
    public async Task Cash_Payment_Should_Generate_FinalFee_Receipt()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var (academicYearId, feeHeadId, studentId, dueFeeId) = await SeedCoreDataAsync();

            var input = MakeInput(studentId, academicYearId, feeHeadId, dueFeeId,
                method: PaymentMethodEnum.Cash,
                bankReceiptConfirmed: null); // must be null for Cash

            var output = await _service.CollectFeeAsync(input);

            output.FeeReceiptNumber.ShouldNotBeNull();
            output.FeeReceiptNumber!.StartsWith("FIN-").ShouldBeTrue(
                $"Cash payment must generate a FinalFee receipt; got: {output.FeeReceiptNumber}");
        }
    }

    // ── Scenario: UPI + BankReceiptConfirmed=true → FinalFee receipt (FIN-*) ─

    [Fact]
    public async Task UPI_BankConfirmed_True_Should_Generate_FinalFee_Receipt()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var (academicYearId, feeHeadId, studentId, dueFeeId) = await SeedCoreDataAsync();

            var input = MakeInput(studentId, academicYearId, feeHeadId, dueFeeId,
                method: PaymentMethodEnum.UPI,
                bankReceiptConfirmed: true,
                details: new() { { "transactionId", "UPI-OK-001" }, { "upiId", "test@upi" } });

            var output = await _service.CollectFeeAsync(input);

            output.FeeReceiptNumber.ShouldNotBeNull();
            output.FeeReceiptNumber!.StartsWith("FIN-").ShouldBeTrue(
                $"UPI with bank confirmed=true must be FinalFee; got: {output.FeeReceiptNumber}");
        }
    }

    // ── Scenario: UPI + BankReceiptConfirmed=false → ProvisionalFee (KFE-*) ──

    [Fact]
    public async Task UPI_BankConfirmed_False_Should_Generate_ProvisionalFee_Receipt()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var (academicYearId, feeHeadId, studentId, dueFeeId) = await SeedCoreDataAsync();

            var input = MakeInput(studentId, academicYearId, feeHeadId, dueFeeId,
                method: PaymentMethodEnum.UPI,
                bankReceiptConfirmed: false,
                details: new() { { "transactionId", "UPI-PEND-001" }, { "upiId", "test@upi" } });

            var output = await _service.CollectFeeAsync(input);

            output.FeeReceiptNumber.ShouldNotBeNull();
            output.FeeReceiptNumber!.StartsWith("KFE-").ShouldBeTrue(
                $"UPI with bank confirmed=false must be ProvisionalFee; got: {output.FeeReceiptNumber}");
        }
    }

    // ── Scenario: CreditCard + BankReceiptConfirmed=false → ProvisionalFee ───

    [Fact]
    public async Task CreditCard_BankConfirmed_False_Should_Generate_ProvisionalFee_Receipt()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var (academicYearId, feeHeadId, studentId, dueFeeId) = await SeedCoreDataAsync();

            var input = MakeInput(studentId, academicYearId, feeHeadId, dueFeeId,
                method: PaymentMethodEnum.CreditCard,
                bankReceiptConfirmed: false,
                details: new() { { "transactionId", "CC001" }, { "cardLast4", "4242" }, { "bankName", "HDFC" } });

            var output = await _service.CollectFeeAsync(input);

            output.FeeReceiptNumber.ShouldNotBeNull();
            output.FeeReceiptNumber!.StartsWith("KFE-").ShouldBeTrue(
                $"CreditCard unconfirmed must be ProvisionalFee; got: {output.FeeReceiptNumber}");
        }
    }

    // ── Scenario: Cheque → always ProvisionalFee (KFE-*), no flag required ───

    [Fact]
    public async Task Cheque_Should_Always_Generate_ProvisionalFee_Receipt()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var (academicYearId, feeHeadId, studentId, dueFeeId) = await SeedCoreDataAsync();

            var input = MakeInput(studentId, academicYearId, feeHeadId, dueFeeId,
                method: PaymentMethodEnum.Cheque,
                bankReceiptConfirmed: null, // must be null for Cheque
                details: new()
                {
                    { "chequeNumber", "000001" },
                    { "bankName", "SBI" },
                    { "chequeDate", DateTime.UtcNow.ToString("O") },
                });

            var output = await _service.CollectFeeAsync(input);

            output.FeeReceiptNumber.ShouldNotBeNull();
            output.FeeReceiptNumber!.StartsWith("KFE-").ShouldBeTrue(
                $"Cheque must always be ProvisionalFee; got: {output.FeeReceiptNumber}");
        }
    }

    // ── Scenario: DemandDraft → always ProvisionalFee (KFE-*) ────────────────

    [Fact]
    public async Task DemandDraft_Should_Always_Generate_ProvisionalFee_Receipt()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var (academicYearId, feeHeadId, studentId, dueFeeId) = await SeedCoreDataAsync();

            var input = MakeInput(studentId, academicYearId, feeHeadId, dueFeeId,
                method: PaymentMethodEnum.DemandDraft,
                bankReceiptConfirmed: null,
                details: new()
                {
                    { "ddNumber", "DD001" },
                    { "bankName", "PNB" },
                    { "ddDate", DateTime.UtcNow.ToString("O") },
                });

            var output = await _service.CollectFeeAsync(input);

            output.FeeReceiptNumber.ShouldNotBeNull();
            output.FeeReceiptNumber!.StartsWith("KFE-").ShouldBeTrue(
                $"DemandDraft must always be ProvisionalFee; got: {output.FeeReceiptNumber}");
        }
    }

    // ── Scenario: DebitCard + confirmed=true → FinalFee ──────────────────────

    [Fact]
    public async Task DebitCard_BankConfirmed_True_Should_Generate_FinalFee_Receipt()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var (academicYearId, feeHeadId, studentId, dueFeeId) = await SeedCoreDataAsync();

            var input = MakeInput(studentId, academicYearId, feeHeadId, dueFeeId,
                method: PaymentMethodEnum.DebitCard,
                bankReceiptConfirmed: true,
                details: new() { { "transactionId", "DC001" }, { "cardLast4", "1234" }, { "bankName", "Axis" } });

            var output = await _service.CollectFeeAsync(input);

            output.FeeReceiptNumber.ShouldNotBeNull();
            output.FeeReceiptNumber!.StartsWith("FIN-").ShouldBeTrue(
                $"DebitCard confirmed must be FinalFee; got: {output.FeeReceiptNumber}");
        }
    }

    // ── Scenario: receipt number uniqueness across sequential calls ───────────

    [Fact]
    public async Task Sequential_Collections_Should_Have_Unique_Receipt_Numbers()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var (academicYearId, feeHeadId, studentId, dueFeeId) = await SeedCoreDataAsync();

            var out1 = await _service.CollectFeeAsync(
                MakeInput(studentId, academicYearId, feeHeadId, dueFeeId, PaymentMethodEnum.Cash, null));
            var out2 = await _service.CollectFeeAsync(
                MakeInput(studentId, academicYearId, feeHeadId, dueFeeId, PaymentMethodEnum.Cash, null));

            out1.FeeReceiptNumber.ShouldNotBe(out2.FeeReceiptNumber,
                "Each collection must produce a unique receipt number");
        }
    }

    // ── Scenario: output contains correct student and transaction info ────────

    [Fact]
    public async Task CollectFee_Should_Return_Correct_Student_And_Year_Ids()
    {
        using (_currentTenant.Change(TestTenantId))
        {
            var (academicYearId, feeHeadId, studentId, dueFeeId) = await SeedCoreDataAsync();

            var input = MakeInput(studentId, academicYearId, feeHeadId, dueFeeId, PaymentMethodEnum.Cash, null);
            var output = await _service.CollectFeeAsync(input);

            output.StudentId.ShouldBe(studentId);
            output.AcademicYearId.ShouldBe(academicYearId);
            output.TotalAmount.ShouldBe(1000d);
            output.PaidFeeHeads.ShouldNotBeEmpty();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly Guid TestTenantId = new Guid("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// Seeds the minimum entities required to call CollectFeeAsync:
    /// AcademicYear → EducationBoard → EducationMedium → EducationMediumStage
    /// → FeeCategory → FeeHead → Student → DueFee.
    /// Returns (academicYearId, feeHeadId, studentId, dueFeeId).
    /// </summary>
    private async Task<(Guid AcademicYearId, Guid FeeHeadId, Guid StudentId, Guid DueFeeId)> SeedCoreDataAsync()
    {
        var yearId    = Guid.NewGuid();
        var boardId   = Guid.NewGuid();
        var mediumId  = Guid.NewGuid();
        var stageId   = Guid.NewGuid();
        var catId     = Guid.NewGuid();
        var feeHeadId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        // 1. AcademicYear — internal constructor, accessible via InternalsVisibleTo
        var year = new AcademicYear(
            yearId, TestTenantId, "2025-26",
            new DateTime(2025, 4, 1), new DateTime(2026, 3, 31));
        await _academicYearRepo.InsertAsync(year, autoSave: true);

        // 2. EducationBoard → AcademicYear
        var board = new EducationBoard(boardId, TestTenantId, EducationBoardType.CBSE, yearId);
        await _boardRepo.InsertAsync(board, autoSave: true);

        // 3. EducationMedium → EducationBoard
        var medium = new EducationMedium(mediumId, TestTenantId, "English", boardId);
        await _mediumRepo.InsertAsync(medium, autoSave: true);

        // 4. EducationMediumStage → EducationMedium
        var stage = new EducationMediumStage(stageId, TestTenantId, "Primary", bannerPicture: null, mediumId);
        await _stageRepo.InsertAsync(stage, autoSave: true);

        // 5. FeeCategory → AcademicYear
        var cat = new FeeCategory(catId, TestTenantId, yearId, "Tuition", "Tuition fees");
        await _feeCategoryRepo.InsertAsync(cat, autoSave: true);

        // 6. FeeHead → FeeCategory
        var feeHead = new FeeHead(feeHeadId, TestTenantId, yearId, catId, "Monthly Fee", "Monthly tuition");
        await _feeHeadRepo.InsertAsync(feeHead, autoSave: true);

        // 7. Student — all required FK entities seeded above
        var student = new Student(
            studentId, TestTenantId,
            "Test", null, "Student",
            yearId, boardId, mediumId, stageId,
            standardId: null, divisionId: null, grNo: 1, rollNo: 1,
            status: StudentLifeCycleStatusEnum.Enrolled);
        await _studentRepo.InsertAsync(student, autoSave: true);

        // 8. DueFee — seed a due fee so we can pay against it
        var dueFeeId = Guid.NewGuid();
        var dueFee = new DueFee(
            id: dueFeeId,
            tenantId: TestTenantId,
            studentId: studentId,
            feeHeadId: feeHeadId,
            month: MonthEnum.June,
            year: 2025,
            isOneTime: false,
            amount: 2000d); // 2000d so sequential payments of 1000d both work
        var dueFeeRepo = GetRequiredService<IDueFeeRepository>();
        await dueFeeRepo.InsertAsync(dueFee, autoSave: true);

        return (yearId, feeHeadId, studentId, dueFeeId);
    }

    private static CollectFeeInput MakeInput(
        Guid studentId,
        Guid academicYearId,
        Guid feeHeadId,
        Guid dueFeeId,
        PaymentMethodEnum method,
        bool? bankReceiptConfirmed,
        Dictionary<string, object?>? details = null)
    {
        return new CollectFeeInput
        {
            StudentId           = studentId,
            AcademicYearId      = academicYearId,
            PaymentConfirmed = bankReceiptConfirmed,
            DueFeePayments      = [new() { DueFeeId = dueFeeId, FeeHeadId = feeHeadId, Amount = 1000d }],
            Payments            =
            [
                new FeePaymentDto
                {
                    PaymentMethod = method,
                    Amount        = 1000d,
                    Details       = details,
                }
            ],
        };
    }
}
