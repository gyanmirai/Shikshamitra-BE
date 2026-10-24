using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Students;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Abp.Identity;

namespace ShikshaMitra.Finance.FeeRefundTests;

/// <summary>
/// Shared test helper for Fee Refund API tests.
/// Builds prerequisite data (student + fee collection with allocations) and
/// convenience factories for refund request DTOs.
/// </summary>
public static class FeeRefundTestHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string FeeRefundUrl = "/api/fee-refunds";
    private const string FeeCollectionUrl = "/api/fee-collections";

    // ── Defaults set by test class ctor ──────────────────────────────────────

    public static Guid DefaultAcademicYearId { get; set; }
    public static Guid DefaultBoardId { get; set; }
    public static Guid DefaultMediumId { get; set; }
    public static Guid DefaultMediumStageId { get; set; }
    public static Guid DefaultStandardId { get; set; }
    public static Guid DefaultDivisionId { get; set; }

    public static async Task<Guid> SeedStudentOnlyAsync(IServiceProvider services)
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        using var scope = services.CreateScope();
        var students = scope.ServiceProvider.GetRequiredService<IRepository<Student, Guid>>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var tenant = currentTenant.Change(tenantId);
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
        var id = Guid.NewGuid();
        var student = Student.CreateDirectRegistration(id, tenantId,
            TestDataHelper.UniqueName("Refund"), null, "Student", DefaultAcademicYearId,
            DefaultBoardId, DefaultMediumId, DefaultMediumStageId, DefaultStandardId,
            DefaultDivisionId, DateTimeOffset.UtcNow.AddYears(-12), GenderEnum.Male,
            TestDataHelper.UniquePhone());
        await students.InsertAsync(student, autoSave: false);
        await uow.CompleteAsync();
        return id;
    }

    public static async Task SeedCheckerScopeAsync(IServiceProvider services, Guid checkerId)
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        using var scope = services.CreateScope();
        var assignments = scope.ServiceProvider.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShikshaMitraDbContext>();
        var scopeService = scope.ServiceProvider.GetRequiredService<IUserScopeService>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var tenant = currentTenant.Change(tenantId);
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
        var userName = $"refund-checker-{checkerId.ToString("N")[..12]}";
        await dbContext.Set<IdentityUser>().AddAsync(new IdentityUser(checkerId, userName,
            $"{userName}@example.test", tenantId));
        await dbContext.SaveChangesAsync();
        await assignments.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenantId,
            checkerId, description: "Refund API checker scope"), autoSave: false);
        await uow.CompleteAsync();
        await scopeService.InvalidateUserScopeCacheAsync(checkerId);
        using var verificationUow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
        var resolved = await scopeService.GetUserScopeAsync(checkerId);
        if (!resolved.HasAnyAccess)
            throw new InvalidOperationException("Seeded refund checker has no effective student scope.");
        await verificationUow.CompleteAsync();
    }

    private static async Task<Guid> SeedFeeHeadAsync(IServiceProvider services)
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        using var scope = services.CreateScope();
        var categories = scope.ServiceProvider.GetRequiredService<IRepository<FeeCategory, Guid>>();
        var incomeCategories = scope.ServiceProvider.GetRequiredService<IRepository<TransactionCategory, Guid>>();
        var heads = scope.ServiceProvider.GetRequiredService<IRepository<FeeHead, Guid>>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var tenant = currentTenant.Change(tenantId);
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
        var feeCategoryId = Guid.NewGuid();
        var incomeCategoryId = Guid.NewGuid();
        var feeHeadId = Guid.NewGuid();
        await incomeCategories.InsertAsync(new TransactionCategory(incomeCategoryId, tenantId,
            DefaultAcademicYearId, TransactionType.Income,
            TestDataHelper.UniqueName("RefundIncome"), "Refund test income category"), autoSave: false);
        await categories.InsertAsync(new FeeCategory(feeCategoryId, tenantId,
            DefaultAcademicYearId, TestDataHelper.UniqueName("RefundFee"),
            "Refund test fee category"), autoSave: false);
        await heads.InsertAsync(new FeeHead(feeHeadId, tenantId, DefaultAcademicYearId,
            feeCategoryId, TestDataHelper.UniqueName("RefundHead"), FeeHeadModule.Finance,
            false, "Refund test fee head", incomeCategoryId), autoSave: false);
        await uow.CompleteAsync();
        return feeHeadId;
    }

    public static async Task<Guid> SeedSchoolBankAccountAsync(IServiceProvider services)
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        using var scope = services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IRepository<BankAccount, Guid>>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var tenant = currentTenant.Change(tenantId);
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
        var id = Guid.NewGuid();
        await accounts.InsertAsync(new BankAccount(id, DefaultAcademicYearId, "Refund Test Bank",
            "Main Branch", null, "ShikshaMitra School", BankAccountType.Current,
            Random.Shared.NextInt64(100000000000, 999999999999).ToString(), null,
            "TEST0001234", null, tenantId: tenantId), autoSave: false);
        await uow.CompleteAsync();
        return id;
    }

    public static async Task<Guid> SeedRefundExpenseCategoryAsync(IServiceProvider services)
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        using var scope = services.CreateScope();
        var categories = scope.ServiceProvider.GetRequiredService<IRepository<TransactionCategory, Guid>>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var tenant = currentTenant.Change(tenantId);
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
        var id = Guid.NewGuid();
        await categories.InsertAsync(new TransactionCategory(id, tenantId, DefaultAcademicYearId,
            TransactionType.Expense, TestDataHelper.UniqueName("RefundExpense"), "Refund test expense category"),
            autoSave: false);
        await uow.CompleteAsync();
        return id;
    }

    public static async Task<Guid> SeedPendingPayoutAsync(IServiceProvider services,
        Guid requestId, Guid userId, RecordRefundPayoutDto input)
    {
        using var scope = services.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>()
            .Change(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>()
            .Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
        var repository = scope.ServiceProvider.GetRequiredService<ShikshaMitra.Finance.Fees.FeeRefund.Repositories.IFeeRefundRepository>();
        var request = (await repository.FindWithLinesAsync(requestId))!;
        var payout = request.RecordPayout(Guid.NewGuid(), userId, input.IdempotencyKey,
            input.Method, input.Amount, input.PaymentDate, input.ExternalReference, null,
            input.BankAccountId, input.EvidenceReference ?? "Existing payment record");
        await repository.StageNewSettlementAsync(payout);
        await uow.CompleteAsync();
        return payout.Id;
    }

    public static async Task<List<ShikshaMitra.Finance.Accounts.Incomes.Transaction>> GetRefundExpensesAsync(
        IServiceProvider services, Guid requestId)
    {
        using var scope = services.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>()
            .Change(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>()
            .Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<ShikshaMitra.Finance.Accounts.Incomes.Transaction, Guid>>();
        return await repository.GetListAsync(x => x.SourceId == requestId);
    }

    // ── Composite seed helper ─────────────────────────────────────────────────

    /// <summary>
    /// Creates a student, a fee head, a DueFee allocation, and a fee collection
    /// that pays the full amount against the DueFee row.
    /// Returns the studentId and the DueFeeId that can be used in refund lines.
    /// </summary>
    public static async Task<(Guid StudentId, Guid DueFeeId)> SeedStudentWithPaidDueFeeAsync(
        HttpClient client,
        IServiceProvider services,
        double feeAmount = 1000d,
        IReadOnlyCollection<RefundPayoutMethod>? allowedPayoutMethods = null, bool includePaidCharges = false, bool allowPartialRefund = true)
    {
        var studentId = await SeedStudentOnlyAsync(services);
        var feeHeadId = await SeedFeeHeadAsync(services);

        // Seed DueFee record first
        var dueFeeId = await FeeCollectionTestHelper.SeedDueFeeAsync(services, studentId, feeHeadId, feeAmount);
        await AttachRefundPolicyAsync(services, studentId, dueFeeId, allowedPayoutMethods, includePaidCharges, allowPartialRefund);

        await SeedSettledCollectionAsync(services, studentId, dueFeeId, feeAmount);

        return (studentId, dueFeeId);
    }

    public static async Task<Guid> SeedStudentWithUnpaidDueFeeAsync(IServiceProvider services)
    {
        var studentId = await SeedStudentOnlyAsync(services);
        var feeHeadId = await SeedFeeHeadAsync(services);
        var dueFeeId = await FeeCollectionTestHelper.SeedDueFeeAsync(services, studentId, feeHeadId, 1000d);
        await AttachRefundPolicyAsync(services, studentId, dueFeeId, null);
        return studentId;
    }

    public static async Task<(Guid AccountId, Guid DueFeeId, Guid ReceiptId, Guid SourceAllocationId)> SeedApplicantWithPaidDueFeeAsync(
        IServiceProvider services, decimal amount = 1000m, bool settled = true)
    {
        var headId = await SeedFeeHeadAsync(services);
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        using var scope = services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var tenant = currentTenant.Change(tenantId);
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
        var policyId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var policy = new RefundPolicy(policyId, tenantId, DefaultAcademicYearId,
            new RefundPolicyUpdateModel($"Applicant refund {policyId:N}", ($"AR-{policyId:N}")[..20],
                "Applicant refund test", RefundPolicyStatus.Active, false, RefundFeeHeadScope.AllFeeHeads,
                [], false, false, true, RefundAmountMode.ManualUpToEligible, null, null, null, null,
                RoundingMethodType.None, RefundDeductionType.None, null, null, null, [RefundPayoutMethod.Cash], 2));
        await scope.ServiceProvider.GetRequiredService<IRepository<RefundPolicy, Guid>>().InsertAsync(policy);
        await scope.ServiceProvider.GetRequiredService<IRepository<FinancePolicyVersion, Guid>>().InsertAsync(
            new FinancePolicyVersion(versionId, tenantId, "refund", policyId, DefaultAcademicYearId,
                policy.Version, policy.PolicyName, policy.Status.ToString(), JsonSerializer.Serialize(policy.ToSnapshot()),
                "created", "Applicant refund fixture"));
        var account = new FinanceFeeAccount(Guid.NewGuid(), tenantId, Guid.NewGuid(), Guid.NewGuid(),
            DefaultAcademicYearId, DefaultBoardId, DefaultMediumId, DefaultMediumStageId, DefaultStandardId, "QA Applicant Refund");
        account.CaptureAssignment(Guid.NewGuid(), 1, JsonSerializer.Serialize(new
        { Policies = new[] { new { Family = "refund", PolicyId = policyId, VersionId = versionId } } }), "refund-fixture");
        await scope.ServiceProvider.GetRequiredService<IRepository<FinanceFeeAccount, Guid>>().InsertAsync(account);
        var due = new DueFee(Guid.NewGuid(), tenantId, null, headId, (MonthEnum)DateTime.UtcNow.Month,
            DateTime.UtcNow.Year, true, amount, DateTime.UtcNow.Date, financeFeeAccountId: account.Id);
        due.ApplyPayment(amount);
        await scope.ServiceProvider.GetRequiredService<IDueFeeRepository>().InsertAsync(due);
        var collection = new FeeRecordMetadata(Guid.NewGuid(), tenantId, null, DefaultAcademicYearId,
            Convert.ToDouble(amount), $"AR-{Guid.NewGuid():N}"[..20], settled ? FeeCollectionStatus.Paid : FeeCollectionStatus.InReconciliation,
            financeFeeAccountId: account.Id);
        await scope.ServiceProvider.GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(collection);
        var allocation = new FeeCollectionDueFeeAllocation(Guid.NewGuid(), tenantId, collection.Id, due.Id, Convert.ToDouble(amount));
        await scope.ServiceProvider.GetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>().InsertAsync(allocation);
        var receipt = new FeePaymentReceipt(Guid.NewGuid())
        {
            TenantId = tenantId, FeeCollectionTransactionId = collection.Id, ReceiptNumber = collection.ReceiptNumber,
            ReceiptKind = settled ? FeeReceiptKind.FinalFee : FeeReceiptKind.ProvisionalFee,
            ReceiptScope = FeeReceiptScope.Fee,
            VerificationStatus = settled ? FeeReceiptVerificationStatus.Verified : FeeReceiptVerificationStatus.PendingVerification,
            TotalPaidAmount = Convert.ToDouble(amount), AcademicYearId = DefaultAcademicYearId,
            BoardId = DefaultBoardId, EducationMediumId = DefaultMediumId, ReceiptDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        receipt.SetFinanceFeeAccount(account.Id);
        await scope.ServiceProvider.GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().InsertAsync(receipt);
        await uow.CompleteAsync();
        return (account.Id, due.Id, receipt.Id, allocation.Id);
    }

    private static async Task SeedSettledCollectionAsync(IServiceProvider services, Guid studentId,
        Guid dueFeeId, double feeAmount)
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        using var scope = services.CreateScope();
        var dueFees = scope.ServiceProvider.GetRequiredService<IDueFeeRepository>();
        var collections = scope.ServiceProvider.GetRequiredService<IRepository<FeeRecordMetadata, Guid>>();
        var allocations = scope.ServiceProvider.GetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>();
        var receipts = scope.ServiceProvider.GetRequiredService<IRepository<FeePaymentReceipt, Guid>>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var tenant = currentTenant.Change(tenantId);
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
        var due = await dueFees.GetAsync(dueFeeId);
        due.ApplyPayment(Convert.ToDecimal(feeAmount));
        var collectionId = Guid.NewGuid();
        var receiptNumber = $"FIN-{Guid.NewGuid().ToString("N")[..16]}";
        var collection = new FeeRecordMetadata(collectionId, tenantId, studentId,
            DefaultAcademicYearId, feeAmount, receiptNumber, FeeCollectionStatus.Paid);
        var allocation = new FeeCollectionDueFeeAllocation(Guid.NewGuid(), tenantId,
            collectionId, dueFeeId, feeAmount);
        var receipt = new FeePaymentReceipt(Guid.NewGuid())
        {
            TenantId = tenantId,
            FeeCollectionTransactionId = collectionId,
            ReceiptNumber = receiptNumber,
            ReceiptKind = FeeReceiptKind.FinalFee,
            ReceiptScope = FeeReceiptScope.Fee,
            VerificationStatus = FeeReceiptVerificationStatus.Verified,
            TotalPaidAmount = feeAmount,
            StudentId = studentId,
            AcademicYearId = DefaultAcademicYearId,
            BoardId = DefaultBoardId,
            EducationMediumId = DefaultMediumId,
            ReceiptDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        await dueFees.UpdateAsync(due, autoSave: false);
        await collections.InsertAsync(collection, autoSave: false);
        await allocations.InsertAsync(allocation, autoSave: false);
        await receipts.InsertAsync(receipt, autoSave: false);
        await uow.CompleteAsync();
    }

    public static async Task<Guid> AdoptLegacyAssignmentAsync(IServiceProvider services, Guid studentId, Guid dueFeeId)
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        using var scope = services.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>()
            .Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
        var dues = scope.ServiceProvider.GetRequiredService<IDueFeeRepository>();
        var due = await dues.GetAsync(dueFeeId);
        var assignments = scope.ServiceProvider.GetRequiredService<IRepository<StudentFeeStructure, Guid>>();
        var assignment = await assignments.GetAsync(due.StudentFeeStructureId!.Value);
        var account = new FinanceFeeAccount(Guid.NewGuid(), tenantId, Guid.NewGuid(), Guid.NewGuid(),
            DefaultAcademicYearId, DefaultBoardId, DefaultMediumId, DefaultMediumStageId, DefaultStandardId,
            "Adopted refund Student", studentId: studentId);
        account.CaptureAssignment(Guid.NewGuid(), 1, JsonSerializer.Serialize(new
        { Policies = new[] { new { Family = "refund", PolicyId = assignment.RefundPolicyId, VersionId = assignment.RefundPolicyVersionId } } }), "adopted-refund-fixture");
        account.BindStudentAssignment(assignment.Id);
        await scope.ServiceProvider.GetRequiredService<IRepository<FinanceFeeAccount, Guid>>().InsertAsync(account);
        assignment.BindFinanceFeeAccount(account.Id);
        await assignments.UpdateAsync(assignment);
        due.BindFinanceFeeAccount(account.Id);
        await dues.UpdateAsync(due);
        await uow.CompleteAsync();
        return account.Id;
    }

    private static async Task AttachRefundPolicyAsync(IServiceProvider services, Guid studentId, Guid dueFeeId,
        IReadOnlyCollection<RefundPayoutMethod>? allowedPayoutMethods, bool includePaidCharges = false, bool allowPartialRefund = true)
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        using var scope = services.CreateScope();
        var policyRepository = scope.ServiceProvider.GetRequiredService<IRepository<RefundPolicy, Guid>>();
        var versionRepository = scope.ServiceProvider.GetRequiredService<IRepository<FinancePolicyVersion, Guid>>();
        var assignmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<StudentFeeStructure, Guid>>();
        var dueFeeRepository = scope.ServiceProvider.GetRequiredService<IDueFeeRepository>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var tenant = currentTenant.Change(tenantId);
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true);

        var policyId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var policy = new RefundPolicy(policyId, tenantId, DefaultAcademicYearId,
            new RefundPolicyUpdateModel(
                $"Refund policy {policyId:N}", ($"RP-{policyId:N}")[..20], "API test policy",
                RefundPolicyStatus.Active, false, RefundFeeHeadScope.AllFeeHeads,
                [], includePaidCharges, false, allowPartialRefund,
                allowPartialRefund ? RefundAmountMode.ManualUpToEligible : RefundAmountMode.RemainingPaidAmount, null, null, null, null,
                RoundingMethodType.None, RefundDeductionType.None, null, null, null,
                allowedPayoutMethods ?? [RefundPayoutMethod.Cash, RefundPayoutMethod.BankTransfer], 2));
        var snapshot = JsonSerializer.Serialize(policy.ToSnapshot());
        var version = new FinancePolicyVersion(versionId, tenantId, "refund", policyId,
            DefaultAcademicYearId, policy.Version, policy.PolicyName, policy.Status.ToString(),
            snapshot, "created", "API test setup");
        var assignment = new StudentFeeStructure(assignmentId, tenantId, studentId, Guid.NewGuid(),
            DefaultAcademicYearId, null, null, ConcessionFeeHeadScope.EntireFeeStructure, [],
            refundPolicyId: policyId, refundPolicyVersionId: versionId);
        var dueFee = await dueFeeRepository.GetAsync(dueFeeId);
        dueFee.BindAssignment(assignmentId);

        await policyRepository.InsertAsync(policy, autoSave: false);
        await versionRepository.InsertAsync(version, autoSave: false);
        await assignmentRepository.InsertAsync(assignment, autoSave: false);
        await dueFeeRepository.UpdateAsync(dueFee, autoSave: false);
        await uow.CompleteAsync();
    }

    public static async Task<(Guid ChargeId, Guid AllocationId, Guid ReceiptId)> SeedPaidChargeAsync(
        IServiceProvider services, Guid studentId, decimal amount = 250m,
        FeeCollectionStatus status = FeeCollectionStatus.Paid, bool combined = true)
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        using var tenant = provider.GetRequiredService<ICurrentTenant>().Change(tenantId);
        using var uow = provider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
        var charge = new ShikshaMitra.Finance.Fees.Charges.Charge(Guid.NewGuid(), tenantId,
            DefaultAcademicYearId, Guid.NewGuid(), "Exam materials", Guid.NewGuid(), studentId,
            DefaultBoardId, DefaultMediumId, DefaultStandardId, DefaultDivisionId, "Refund charge fixture",
            amount, DateTimeOffset.UtcNow.AddDays(2), ShikshaMitra.Finance.Fees.Charges.ChargePriority.Medium,
            ShikshaMitra.Finance.Fees.Charges.ChargeStatus.Approved, Guid.NewGuid());
        await provider.GetRequiredService<IRepository<ShikshaMitra.Finance.Fees.Charges.Charge, Guid>>().InsertAsync(charge);
        var collections = provider.GetRequiredService<IRepository<FeeRecordMetadata, Guid>>();
        var receipts = provider.GetRequiredService<IRepository<FeePaymentReceipt, Guid>>();
        var receipt = combined ? (await receipts.GetListAsync(x => x.StudentId == studentId)).Single() : null;
        var collection = receipt == null ? new FeeRecordMetadata(Guid.NewGuid(), tenantId, studentId,
            DefaultAcademicYearId, (double)amount, $"CH-{Guid.NewGuid():N}"[..20], status)
            : await collections.GetAsync(receipt.FeeCollectionTransactionId);
        if (receipt == null)
        {
            await collections.InsertAsync(collection);
            receipt = new FeePaymentReceipt(Guid.NewGuid())
            {
                TenantId = tenantId, StudentId = studentId, AcademicYearId = DefaultAcademicYearId,
                FeeCollectionTransactionId = collection.Id, ReceiptNumber = collection.ReceiptNumber,
                ReceiptScope = FeeReceiptScope.Fee, ReceiptKind = status == FeeCollectionStatus.InReconciliation
                    ? FeeReceiptKind.ProvisionalFee : FeeReceiptKind.FinalFee,
                VerificationStatus = status == FeeCollectionStatus.InReconciliation
                    ? FeeReceiptVerificationStatus.PendingVerification : FeeReceiptVerificationStatus.Verified,
                TotalPaidAmount = (double)amount, BoardId = DefaultBoardId, EducationMediumId = DefaultMediumId,
                ReceiptDate = DateOnly.FromDateTime(DateTime.UtcNow)
            };
            await receipts.InsertAsync(receipt);
        }
        else
        {
            receipt.ReceiptScope = FeeReceiptScope.Combined;
            receipt.TotalPaidAmount += (double)amount;
            await receipts.UpdateAsync(receipt);
        }
        var allocation = new ChargeCollectionRecord(Guid.NewGuid())
        {
            TenantId = tenantId, StudentId = studentId, ChargeId = charge.Id, Amount = (double)amount,
            PaymentDate = DateTimeOffset.UtcNow, FeeRecordMetadataId = collection.Id
        };
        await provider.GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>().InsertAsync(allocation);
        await uow.CompleteAsync();
        return (charge.Id, allocation.Id, receipt.Id);
    }

    public static async Task<RefundEligibilityMonthDto> GetEligibleLineAsync(
        HttpClient client, Guid studentId, Guid dueFeeId)
    {
        var response = await client.GetAsync(
            $"{FeeRefundUrl}/eligibility?studentId={studentId}&academicYearId={DefaultAcademicYearId}");
        response.EnsureSuccessStatusCode();
        var eligibility = await response.Content.ReadFromJsonAsync<RefundEligibilityDto>(JsonOptions)
            ?? throw new InvalidOperationException("Null refund eligibility response");
        return eligibility.FeeHeads.SelectMany(x => x.Months).Single(x => x.DueFeeId == dueFeeId);
    }

    /// <summary>
    /// Creates a refund request against a specific DueFee row and returns the created DTO.
    /// Requires that the DueFee row already has a positive refundable balance.
    /// </summary>
    public static async Task<FeeRefundRequestDto> CreateRefundRequestAsync(
        HttpClient client,
        Guid studentId,
        Guid dueFeeId,
        double amount = 500d,
        string method = "Cash",
        Guid? replacesRefundRequestId = null)
    {
        var eligible = await GetEligibleLineAsync(client, studentId, dueFeeId);
        var input = new CreateFeeRefundRequestDto
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            StudentId = studentId,
            AcademicYearId = DefaultAcademicYearId,
            OriginalReceiptId = eligible.SourceReceiptId,
            RefundMethod = method,
            ReplacesRefundRequestId = replacesRefundRequestId,
            PaymentDetails = method == "Cash"
                ? new Dictionary<string, object?>
                {
                    ["refundDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    ["receivedBy"] = "Test parent",
                }
                : new Dictionary<string, object?>
                {
                    ["beneficiaryName"] = "Test parent",
                    ["beneficiaryBankName"] = "Test Bank",
                    ["beneficiaryAccountNumber"] = "123456789012",
                    ["ifscCode"] = "TEST0001234",
                },
            Reason = "Test refund",
            Lines = new List<CreateFeeRefundLineDto>
            {
                new()
                {
                    DueFeeId = dueFeeId, SourceAllocationId = eligible.SourceAllocationId,
                    Amount = Convert.ToDecimal(amount)
                },
            },
        };

        var response = await client.PostAsJsonAsync(FeeRefundUrl, input, JsonOptions);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Refund request failed ({(int)response.StatusCode}): " +
                await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions)
            ?? throw new InvalidOperationException("Null FeeRefundRequestDto from response");
    }
}
