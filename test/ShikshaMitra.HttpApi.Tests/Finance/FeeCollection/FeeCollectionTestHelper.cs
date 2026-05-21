using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections.Dtos;

namespace ShikshaMitra.Finance.FeeCollectionTests;

/// <summary>
/// Shared test helper for Fee Collection API tests.
/// Builds prerequisite data and convenience factories for request DTOs.
/// </summary>
public static class FeeCollectionTestHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string FeeCollectionUrl = "/api/v2/fee-collections";

    // ── Defaults set by the test class ctor ──────────────────────────────────

    public static Guid DefaultAcademicYearId { get; set; }
    public static Guid DefaultMediumId { get; set; }
    public static Guid DefaultStandardId { get; set; }
    public static Guid DefaultDivisionId { get; set; }

    // ── Data builders ─────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a student and a fee head, then records a cash payment.
    /// Returns the <see cref="CollectFeeOutput"/> from the API.
    /// </summary>
    public static async Task<(CollectFeeOutput Output, Guid StudentId, Guid BoardId)> CollectFeeAsync(
        HttpClient client,
        double feeAmount = 1000d)
    {
        var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(client);
        var feeHeadId = await CreateFeeHeadAsync(client);

        var input = BuildCollectFeeInput(studentId, DefaultAcademicYearId, feeHeadId, feeAmount);

        var response = await client.PostAsJsonAsync(FeeCollectionUrl, input, JsonOptions);
        response.EnsureSuccessStatusCode();

        var output = await response.Content.ReadFromJsonAsync<CollectFeeOutput>(JsonOptions)
            ?? throw new InvalidOperationException("Null CollectFeeOutput from response");

        return (output, studentId, boardId);
    }

    /// <summary>
    /// Creates a fee category and a fee head under it, returning the fee head ID.
    /// </summary>
    public static async Task<Guid> CreateFeeHeadAsync(HttpClient client)
    {
        var feeCategoryId = await ConcessionRequestTestHelper.CreateFeeCategoryAsync(client);
        return await ConcessionRequestTestHelper.CreateFeeHeadAsync(client, feeCategoryId);
    }

    /// <summary>
    /// Builds a <see cref="CollectFeeInput"/> with a single cash payment.
    /// </summary>
    public static CollectFeeInput BuildCollectFeeInput(
        Guid studentId,
        Guid academicYearId,
        Guid feeHeadId,
        double amount = 1000d,
        double? receivedAmount = null)
    {
        return new CollectFeeInput
        {
            StudentId = studentId,
            AcademicYearId = academicYearId,
            TotalAmount = amount,
            ReceivedAmount = receivedAmount,
            Notes = "Test payment",
            SelectedFeeHeads = new List<SelectedFeeHeadDto>
            {
                new() { FeeHeadId = feeHeadId, Amount = amount },
            },
            Payments = new List<FeePaymentDto>
            {
                new()
                {
                    PaymentMethod = PaymentMethodEnum.Cash,
                    Amount = receivedAmount ?? amount,
                },
            },
        };
    }
}
