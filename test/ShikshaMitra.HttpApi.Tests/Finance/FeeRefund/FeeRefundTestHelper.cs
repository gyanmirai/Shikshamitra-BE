using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Finance.Fees.Collections;

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
        var input = new CreateFeeRefundRequestDto
        {
            StudentId = studentId,
            AcademicYearId = DefaultAcademicYearId,
            RefundMethod = "Cash",
            Reason = "Test refund",
            Lines = new List<CreateFeeRefundLineDto>
            {
                new() { DueFeeId = dueFeeId, Amount = amount },
            },
        };

        var response = await client.PostAsJsonAsync(FeeRefundUrl, input, JsonOptions);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions)
               ?? throw new InvalidOperationException("Null FeeRefundRequestDto from response");
    }
}
