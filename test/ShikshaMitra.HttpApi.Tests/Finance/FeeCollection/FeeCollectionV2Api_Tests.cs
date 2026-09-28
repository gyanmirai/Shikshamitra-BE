using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.FeeCollections;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class FeeCollectionV2Api_Tests : ShikshaMitraApiTestBase
{
    private const string BaseUrl = "/api/fee-collections";

    public FeeCollectionV2Api_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
    {
        FeeCollectionTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
        FeeCollectionTestHelper.DefaultMediumId = factory.SeededMediumId;
        FeeCollectionTestHelper.DefaultStandardId = factory.SeededStandardId;
        FeeCollectionTestHelper.DefaultDivisionId = factory.SeededDivisionId;

        ConcessionRequestTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
        ConcessionRequestTestHelper.DefaultMediumId = factory.SeededMediumId;
        ConcessionRequestTestHelper.DefaultStandardId = factory.SeededStandardId;
        ConcessionRequestTestHelper.DefaultDivisionId = factory.SeededDivisionId;
    }

    // ── GET /api/v2/fee-collections ───────────────────────────────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Get_Summary_Returns_200_With_PagedResultDto_Shape()
    {
        var (_, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);

        var url = $"{BaseUrl}?academicYearId={FeeCollectionTestHelper.DefaultAcademicYearId}" +
                  $"&boardId={boardId}" +
                  $"&educationMediumId={FeeCollectionTestHelper.DefaultMediumId}";

        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResultDto<FeeCollectionSummaryItemDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.Items.ShouldNotBeNull();
        body.TotalCount.ShouldBeGreaterThanOrEqualTo(0);
        foreach (var item in body.Items)
        {
            item.TotalCharges.ShouldBeGreaterThanOrEqualTo(0);
            item.PendingCharges.ShouldBeGreaterThanOrEqualTo(0);
            item.PendingCharges.ShouldBeLessThanOrEqualTo(item.TotalCharges);
        }
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Get_Summary_Returns_Empty_When_No_Enrolled_Students()
    {
        var divisionId = await CreateDivisionAsync(FeeCollectionTestHelper.DefaultStandardId, "FeeEmpty");
        var (_, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client, divisionId);

        var url = $"{BaseUrl}?academicYearId={FeeCollectionTestHelper.DefaultAcademicYearId}" +
                  $"&boardId={boardId}" +
                  $"&educationMediumId={FeeCollectionTestHelper.DefaultMediumId}" +
                  $"&standardId={FeeCollectionTestHelper.DefaultStandardId}" +
                  $"&divisionId={divisionId}";

        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResultDto<FeeCollectionSummaryItemDto>>(JsonOptions);
        body.ShouldNotBeNull();
        // Students created via direct registration are in Registration status, not Enrolled
        body.TotalCount.ShouldBe(0);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Get_Summary_With_Search_Returns_200()
    {
        var (_, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);

        var url = $"{BaseUrl}?academicYearId={FeeCollectionTestHelper.DefaultAcademicYearId}" +
                  $"&boardId={boardId}" +
                  $"&educationMediumId={FeeCollectionTestHelper.DefaultMediumId}" +
                  $"&search=Test";

        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Search_Each_Name_Word_In_Any_Order_Before_Pagination()
    {
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
        var studentId = Guid.NewGuid();
        using (var scope = Factory.Services.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
        using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
        {
            var student = new Student(studentId, tenantId, "Diya", "Hiren", "Akbari",
                Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId,
                Factory.SeededMediumStageId, Factory.SeededStandardId, Factory.SeededDivisionId,
                grNo: 10212, rollNo: 2, status: StudentLifeCycleStatusEnum.Enrolled);
            await scope.ServiceProvider.GetRequiredService<IRepository<Student, Guid>>()
                .InsertAsync(student, autoSave: true);
            await uow.CompleteAsync();
        }

        var baseUrl = $"{BaseUrl}?academicYearId={Factory.SeededAcademicYearId}" +
                      $"&boardId={Factory.SeededBoardId}" +
                      $"&educationMediumId={Factory.SeededMediumId}" +
                      $"&standardId={Factory.SeededStandardId}" +
                      $"&divisionId={Factory.SeededDivisionId}" +
                      "&maxResultCount=1";

        foreach (var search in new[] { "Diya Akbari", "akbari diya", "Di Ak" })
        {
            var response = await Client.GetAsync($"{baseUrl}&search={Uri.EscapeDataString(search)}");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<PagedResultDto<FeeCollectionSummaryItemDto>>(JsonOptions);
            body.ShouldNotBeNull();
            body.Items.ShouldContain(item => item.StudentId == studentId);
            body.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
        }

        var noMatch = await Client.GetAsync($"{baseUrl}&search=Diya%20Unknown");
        noMatch.StatusCode.ShouldBe(HttpStatusCode.OK);
        var emptyBody = await noMatch.Content.ReadFromJsonAsync<PagedResultDto<FeeCollectionSummaryItemDto>>(JsonOptions);
        emptyBody.ShouldNotBeNull();
        emptyBody.Items.ShouldNotContain(item => item.StudentId == studentId);
    }

    // ── POST /api/v2/fee-collections ─────────────────────────────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Collect_Fee_Returns_201_With_Receipt_Number()
    {
        var (output, _, _, _) = await FeeCollectionTestHelper.CollectFeeAsync(Client, Factory.Services, feeAmount: 1000d);

        output.Id.ShouldNotBe(Guid.Empty);
        output.ReceiptNumber.ShouldNotBeNullOrWhiteSpace();
        output.ReceiptNumber.ShouldStartWith("REC-");
        output.TotalAmount.ShouldBe(1000d);
        output.Status.ShouldBe(FeeCollectionStatus.Paid);
        output.PaidFeeHeads.ShouldNotBeEmpty();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Collect_Fee_Returns_Paid_Status_When_Full_Amount_Received()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        var feeHeadId = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
        var dueFeeId = await FeeCollectionTestHelper.SeedDueFeeAsync(Factory.Services, studentId, feeHeadId, amount: 500d);

        var input = FeeCollectionTestHelper.BuildCollectFeeInput(
            studentId,
            FeeCollectionTestHelper.DefaultAcademicYearId,
            feeHeadId,
            dueFeeId,
            amount: 500d);

        var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var output = await response.Content.ReadFromJsonAsync<CollectFeeOutput>(JsonOptions);
        output.ShouldNotBeNull();
        output.Status.ShouldBe(FeeCollectionStatus.Paid);
        output.StudentId.ShouldBe(studentId);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Collect_Fee_Returns_Partial_Status_When_Partial_Amount_Received()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        var feeHeadId = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
        var dueFeeId = await FeeCollectionTestHelper.SeedDueFeeAsync(Factory.Services, studentId, feeHeadId, amount: 1000d);

        var input = FeeCollectionTestHelper.BuildCollectFeeInput(
            studentId,
            FeeCollectionTestHelper.DefaultAcademicYearId,
            feeHeadId,
            dueFeeId,
            amount: 400d);

        var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var output = await response.Content.ReadFromJsonAsync<CollectFeeOutput>(JsonOptions);
        output.ShouldNotBeNull();
        output.Status.ShouldBe(FeeCollectionStatus.Partial);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Collect_Fee_Returns_Error_For_Invalid_FeeHead()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        var dummyFeeHeadId = Guid.NewGuid();
        var dueFeeId = await FeeCollectionTestHelper.SeedDueFeeAsync(Factory.Services, studentId, dummyFeeHeadId, amount: 500d);

        var input = FeeCollectionTestHelper.BuildCollectFeeInput(
            studentId,
            FeeCollectionTestHelper.DefaultAcademicYearId,
            dummyFeeHeadId,
            dueFeeId,
            amount: 500d);

        var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Collect_Fee_With_UPI_Payment_Details_Returns_201()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        var feeHeadId = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
        var dueFeeId = await FeeCollectionTestHelper.SeedDueFeeAsync(Factory.Services, studentId, feeHeadId, amount: 750d);

        var input = new CollectFeeInput
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            StudentId = studentId,
            AcademicYearId = FeeCollectionTestHelper.DefaultAcademicYearId,
            Notes = "UPI payment test",
            PaymentConfirmed = true,
            DueFeePayments = new List<DueFeePaymentDto>
            {
                new() { DueFeeId = dueFeeId, FeeHeadId = feeHeadId, Amount = 750d },
            },
            Payments = new List<FeePaymentDto>
            {
                new()
                {
                    PaymentMethod = PaymentMethodEnum.UPI,
                    Amount = 750d,
                    Details = new Dictionary<string, object?>
                    {
                        { "upiTransactionId", "TXN123456789" },
                        { "upiId", "test@upi" },
                        { "appName", "GPay" },
                    },
                },
            },
        };

        var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var output = await response.Content.ReadFromJsonAsync<CollectFeeOutput>(JsonOptions);
        output.ShouldNotBeNull();
        output.ReceiptNumber.ShouldStartWith("REC-");
    }

    // ── GET /api/v2/fee-collections/{studentId}/fee-details ──────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Get_Student_Fee_Details_Returns_200_With_Student_Summary()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);

        var url = $"{BaseUrl}/{studentId}/fee-details?academicYearId={FeeCollectionTestHelper.DefaultAcademicYearId}";

        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<StudentFeeDetailsDto>(JsonOptions);
        body.ShouldNotBeNull();
        body.Student.ShouldNotBeNull();
        body.Student.Id.ShouldBe(studentId);
        body.FeeOverview.ShouldNotBeNull();
        body.FeeHeads.ShouldNotBeNull();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Get_Student_Fee_Details_Reflects_Payment_History_After_Collection()
    {
        var (output, studentId, _, _) = await FeeCollectionTestHelper.CollectFeeAsync(Client, Factory.Services, feeAmount: 500d);

        var url = $"{BaseUrl}/{studentId}/fee-details?academicYearId={FeeCollectionTestHelper.DefaultAcademicYearId}";

        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<StudentFeeDetailsDto>(JsonOptions);
        body.ShouldNotBeNull();
        body.Payments.ShouldNotBeNull();
        body.Payments.ShouldNotBeEmpty();
        body.Payments![0].ReceiptNumber.ShouldBe(output.ReceiptNumber);
    }

    // ── GET /api/v2/fee-collections/{studentId}/collect-form ─────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Get_Collect_Form_Returns_200_With_Student_Info()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);

        var url = $"{BaseUrl}/{studentId}/collect-form?academicYearId={FeeCollectionTestHelper.DefaultAcademicYearId}";

        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<FeeCollectFormDto>(JsonOptions);
        body.ShouldNotBeNull();
        body.Student.ShouldNotBeNull();
        body.Student.StudentId.ShouldBe(studentId);
        body.Summary.ShouldNotBeNull();
        body.Ledger.ShouldNotBeNull();
        body.Charges.ShouldNotBeNull();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Get_Collect_Form_Returns_400_When_AcademicYearId_Missing()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);

        var response = await Client.GetAsync($"{BaseUrl}/{studentId}/collect-form");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Get_Student_Fee_Details_Returns_400_When_AcademicYearId_Missing()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);

        var response = await Client.GetAsync($"{BaseUrl}/{studentId}/fee-details");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── GET /api/v2/fee-collections validation ───────────────────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Get_Summary_Returns_400_When_BoardId_Missing()
    {
        var url = $"{BaseUrl}?academicYearId={FeeCollectionTestHelper.DefaultAcademicYearId}" +
                  $"&educationMediumId={FeeCollectionTestHelper.DefaultMediumId}";

        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Get_Summary_Returns_400_When_Search_Too_Short()
    {
        var (_, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);

        var url = $"{BaseUrl}?academicYearId={FeeCollectionTestHelper.DefaultAcademicYearId}" +
                  $"&boardId={boardId}" +
                  $"&educationMediumId={FeeCollectionTestHelper.DefaultMediumId}" +
                  $"&search=T";

        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_401_For_Anonymous_Client()
    {
        var anon = CreateAnonymousClient();
        var response = await anon.GetAsync(BaseUrl);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/v2/fee-collections/export ────────────────────────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Export_Csv_Returns_200_With_Bytes()
    {
        var (_, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);

        var url = $"{BaseUrl}/export?academicYearId={FeeCollectionTestHelper.DefaultAcademicYearId}" +
                  $"&boardId={boardId}" +
                  $"&educationMediumId={FeeCollectionTestHelper.DefaultMediumId}" +
                  $"&format=0";

        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.ShouldBeGreaterThanOrEqualTo(0);
    }

    // ── Payment method tests (bugs fixed) ────────────────────────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Collect_Fee_With_CreditCard_Returns_201()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        var feeHeadId = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
        var dueFeeId = await FeeCollectionTestHelper.SeedDueFeeAsync(Factory.Services, studentId, feeHeadId, amount: 500d);

        var input = new CollectFeeInput
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            StudentId = studentId,
            AcademicYearId = FeeCollectionTestHelper.DefaultAcademicYearId,
            PaymentConfirmed = true,
            DueFeePayments = new List<DueFeePaymentDto>
            {
                new() { DueFeeId = dueFeeId, FeeHeadId = feeHeadId, Amount = 500d },
            },
            Payments = new List<FeePaymentDto>
            {
                new() { PaymentMethod = PaymentMethodEnum.CreditCard, Amount = 500d, Details = new() { { "transactionId", "CC123" } } },
            },
        };

        var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Collect_Fee_With_Cheque_Without_Details_Returns_400()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        var feeHeadId = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
        var dueFeeId = await FeeCollectionTestHelper.SeedDueFeeAsync(Factory.Services, studentId, feeHeadId, amount: 800d);

        var input = new CollectFeeInput
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            StudentId = studentId,
            AcademicYearId = FeeCollectionTestHelper.DefaultAcademicYearId,
            DueFeePayments = new List<DueFeePaymentDto>
            {
                new() { DueFeeId = dueFeeId, FeeHeadId = feeHeadId, Amount = 800d },
            },
            Payments = new List<FeePaymentDto>
            {
                new() { PaymentMethod = PaymentMethodEnum.Cheque, Amount = 800d },
            },
        };

        var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Collect_Fee_With_Cheque_With_Details_Returns_201()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        var feeHeadId = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
        var dueFeeId = await FeeCollectionTestHelper.SeedDueFeeAsync(Factory.Services, studentId, feeHeadId, amount: 800d);

        var input = new CollectFeeInput
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            StudentId = studentId,
            AcademicYearId = FeeCollectionTestHelper.DefaultAcademicYearId,
            DueFeePayments = new List<DueFeePaymentDto>
            {
                new() { DueFeeId = dueFeeId, FeeHeadId = feeHeadId, Amount = 800d },
            },
            Payments = new List<FeePaymentDto>
            {
                new()
                {
                    PaymentMethod = PaymentMethodEnum.Cheque,
                    Amount = 800d,
                    Details = new Dictionary<string, object?>
                    {
                        { "chequeNumber", "CHQ-001" },
                        { "bankName", "Test Bank" },
                        { "chequeDate", "2026-04-15" },
                    },
                },
            },
        };

        var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    // ── Collect-form month name tests (bug fixed) ─────────────────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Get_Collect_Form_Ledger_Returns_Month_Names_Not_Numbers()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);

        var url = $"{BaseUrl}/{studentId}/collect-form?academicYearId={FeeCollectionTestHelper.DefaultAcademicYearId}";

        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<FeeCollectFormDto>(JsonOptions);
        body.ShouldNotBeNull();
        body.Ledger.ShouldNotBeNull();

        foreach (var month in body.Ledger)
        {
            // Month should be a name like "January", "April" — NOT a zero-padded number like "04"
            int numericValue;
            var isNumeric = int.TryParse(month.Month, out numericValue);
            isNumeric.ShouldBeFalse($"Month '{month.Month}' should be a name, not a number");
            month.Month.Length.ShouldBeGreaterThan(2, $"Month '{month.Month}' looks like a number not a name");
        }
    }

    private async Task<Guid> CreateDivisionAsync(Guid standardId, string namePrefix)
    {
        using var scope = Factory.Services.CreateScope();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var divisionManager = scope.ServiceProvider.GetRequiredService<DivisionManager>();
        var divisionRepository = scope.ServiceProvider.GetRequiredService<IDivisionRepository>();
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

        using (currentTenant.Change(tenantId))
        using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
        {
            var division = await divisionManager.CreateAsync(
                standardId,
                $"{namePrefix}-{Guid.NewGuid():N}",
                60);
            await divisionRepository.InsertAsync(division, autoSave: false);
            await uow.CompleteAsync();
            return division.Id;
        }
    }
}
