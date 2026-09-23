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
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

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

    // ── Composite seed helper ─────────────────────────────────────────────────

    /// <summary>
    /// Creates a student, a fee head, a DueFee allocation, and a fee collection
    /// that pays the full amount against the DueFee row.
    /// Returns the studentId and the DueFeeId that can be used in refund lines.
    /// </summary>
    public static async Task<(Guid StudentId, Guid DueFeeId)> SeedStudentWithPaidDueFeeAsync(
        HttpClient client,
        IServiceProvider services,
        double feeAmount = 1000d)
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(client);
        var feeHeadId = await FeeCollectionTestHelper.CreateFeeHeadAsync(client);

        // Seed DueFee record first
        var dueFeeId = await FeeCollectionTestHelper.SeedDueFeeAsync(services, studentId, feeHeadId, feeAmount);
        await AttachRefundPolicyAsync(services, studentId, dueFeeId);

        // Collect fee with explicit allocation to DueFee row
        var collectInput = new CollectFeeInput
        {
            StudentId = studentId,
            AcademicYearId = DefaultAcademicYearId,
            Notes = "Test payment with allocation",
            DueFeePayments = new List<DueFeePaymentDto>
            {
                new() { DueFeeId = dueFeeId, FeeHeadId = feeHeadId, Amount = feeAmount },
            },
            Payments = new List<FeePaymentDto>
            {
                new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = feeAmount },
            },
        };

        var collectResp = await client.PostAsJsonAsync(FeeCollectionUrl, collectInput, JsonOptions);
        collectResp.EnsureSuccessStatusCode();

        return (studentId, dueFeeId);
    }

    private static async Task AttachRefundPolicyAsync(IServiceProvider services, Guid studentId, Guid dueFeeId)
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
                $"Refund policy {policyId:N}", $"RP-{policyId:N}", "API test policy",
                RefundPolicyStatus.Active, false, RefundFeeHeadScope.AllFeeHeads,
                [], false, false, true,
                RefundAmountMode.ManualUpToEligible, null, null, null, null,
                RoundingMethodType.None, RefundDeductionType.None, null, null, null,
                [RefundPayoutMethod.Cash], 2));
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
        double amount = 500d)
    {
        var eligible = await GetEligibleLineAsync(client, studentId, dueFeeId);
        var input = new CreateFeeRefundRequestDto
        {
            StudentId = studentId,
            AcademicYearId = DefaultAcademicYearId,
            OriginalReceiptId = eligible.SourceReceiptId,
            RefundMethod = "Cash",
            PaymentDetails = new Dictionary<string, object?>
            {
                ["refundDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                ["receivedBy"] = "Test parent",
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
        response.EnsureSuccessStatusCode();

        var draft = await response.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions)
            ?? throw new InvalidOperationException("Null FeeRefundRequestDto from response");
        var submit = await client.PostAsync($"{FeeRefundUrl}/{draft.Id}/submit", null);
        submit.EnsureSuccessStatusCode();
        return await submit.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions)
            ?? throw new InvalidOperationException("Null submitted FeeRefundRequestDto from response");
    }
}
