using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.FeeCollectionTests;

/// <summary>
/// Shared test helper for Fee Collection API tests.
/// Builds prerequisite data and convenience factories for request DTOs.
/// </summary>
public static class FeeCollectionTestHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string FeeCollectionUrl = "/api/fee-collections";

    // ── Defaults set by the test class ctor ──────────────────────────────────

    public static Guid DefaultAcademicYearId { get; set; }
    public static Guid DefaultMediumId { get; set; }
    public static Guid DefaultStandardId { get; set; }
    public static Guid DefaultDivisionId { get; set; }

    // ── Data builders ─────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a student and a fee head, seeds a DueFee, then records a cash payment.
    /// Returns the <see cref="CollectFeeOutput"/> from the API.
    /// </summary>
    public static async Task<(CollectFeeOutput Output, Guid StudentId, Guid BoardId, Guid DueFeeId)> CollectFeeAsync(
        HttpClient client,
        IServiceProvider services,
        double feeAmount = 1000d)
    {
        var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(client);
        var feeHeadId = await CreateFeeHeadAsync(client);

        var dueFeeId = await SeedDueFeeAsync(services, studentId, feeHeadId, feeAmount);

        var input = BuildCollectFeeInput(studentId, DefaultAcademicYearId, feeHeadId, dueFeeId, feeAmount);

        var response = await client.PostAsJsonAsync(FeeCollectionUrl, input, JsonOptions);
        response.EnsureSuccessStatusCode();

        var output = await response.Content.ReadFromJsonAsync<CollectFeeOutput>(JsonOptions)
            ?? throw new InvalidOperationException("Null CollectFeeOutput from response");

        return (output, studentId, boardId, dueFeeId);
    }

    /// <summary>
    /// Seeds a DueFee obligation directly in the database.
    /// </summary>
    public static async Task<Guid> SeedDueFeeAsync(
        IServiceProvider services,
        Guid studentId,
        Guid feeHeadId,
        double amount = 1000d)
    {
        using var scope = services.CreateScope();
        var dueFeeRepo = scope.ServiceProvider.GetRequiredService<IDueFeeRepository>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        var dueFeeId = Guid.NewGuid();
        using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
        using (currentTenant.Change(Guid.Parse("11111111-1111-1111-1111-111111111111")))
        {
            var dueFee = new DueFee(
                id: dueFeeId,
                tenantId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                studentId: studentId,
                feeHeadId: feeHeadId,
                month: MonthEnum.June,
                year: 2025,
                isOneTime: false,
                amount: (decimal)amount,
                dueDate: new DateTime(2025, 6, 10));

            await dueFeeRepo.InsertAsync(dueFee, autoSave: true);
            await uow.CompleteAsync();
        }

        return dueFeeId;
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
        Guid dueFeeId,
        double amount = 1000d)
    {
        return new CollectFeeInput
        {
            StudentId = studentId,
            AcademicYearId = academicYearId,
            Notes = "Test payment",
            DueFeePayments = new List<DueFeePaymentDto>
            {
                new() { DueFeeId = dueFeeId, FeeHeadId = feeHeadId, Amount = amount },
            },
            Payments = new List<FeePaymentDto>
            {
                new()
                {
                    PaymentMethod = PaymentMethodEnum.Cash,
                    Amount = amount,
                },
            },
        };
    }
}
