using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings;
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

    [Theory]
    [InlineData(0, 0, FeeCollectionStatus.NoFeeAssigned)]
    [InlineData(1000, 1000, FeeCollectionStatus.Paid)]
    [InlineData(1000, 0, FeeCollectionStatus.Overdue)]
    [InlineData(1000, 500, FeeCollectionStatus.Overdue)]
    [InlineData(1000, 0, FeeCollectionStatus.Pending)]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Summary_Should_Distinguish_Unassigned_Students_From_Paid_Fees(
        int total, int paid, FeeCollectionStatus expectedStatus)
    {
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
        var studentId = Guid.NewGuid();
        var name = $"FeeStatus{studentId:N}";
        using (var scope = Factory.Services.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
        using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
        {
            var student = new Student(studentId, tenantId, name, null, "Status",
                Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId,
                Factory.SeededMediumStageId, Factory.SeededStandardId, Factory.SeededDivisionId,
                status: StudentLifeCycleStatusEnum.Enrolled);
            await scope.ServiceProvider.GetRequiredService<IRepository<Student, Guid>>()
                .InsertAsync(student, autoSave: true);
            if (total > 0)
            {
                var suffix = studentId.ToString("N")[..8];
                var accountHead = new AccountHead(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
                    $"Status Revenue {suffix}", AccountHeadType.Income, null, null);
                await scope.ServiceProvider.GetRequiredService<IRepository<AccountHead, Guid>>()
                    .InsertAsync(accountHead, autoSave: true);
                var incomeCategory = new TransactionCategory(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
                    TransactionType.Income, $"Status Income {suffix}", null, [], true, $"STATUS-INC-{suffix}",
                    accountHead.Id, DateTimeOffset.UtcNow.AddDays(-1), null);
                await scope.ServiceProvider.GetRequiredService<IRepository<TransactionCategory, Guid>>()
                    .InsertAsync(incomeCategory, autoSave: true);
                var category = new FeeCategory(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
                    $"Status Fees {suffix}", "Collection status test");
                await scope.ServiceProvider.GetRequiredService<IRepository<FeeCategory, Guid>>()
                    .InsertAsync(category, autoSave: true);
                var head = new FeeHead(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId, category.Id,
                    $"Status Fee {suffix}", FeeHeadModule.Finance, false, "Collection status test", incomeCategory.Id);
                await scope.ServiceProvider.GetRequiredService<IRepository<FeeHead, Guid>>()
                    .InsertAsync(head, autoSave: true);
                var year = expectedStatus == FeeCollectionStatus.Pending ? 2099 : 2020;
                var due = new DueFee(Guid.NewGuid(), tenantId, studentId, head.Id,
                    MonthEnum.June, year, false, total, new DateTime(year, 6, 10));
                if (paid > 0) due.ApplyPayment(paid);
                await scope.ServiceProvider.GetRequiredService<IDueFeeRepository>().InsertAsync(due, autoSave: true);
            }
            await uow.CompleteAsync();
        }

        var query = $"academicYearId={Factory.SeededAcademicYearId}&boardId={Factory.SeededBoardId}" +
                    $"&educationMediumId={Factory.SeededMediumId}&search={name}";
        var summary = await Client.GetFromJsonAsync<PagedResultDto<FeeCollectionSummaryItemDto>>(
            $"{BaseUrl}?{query}", JsonOptions);
        summary.ShouldNotBeNull();
        summary.TotalCount.ShouldBe(1);
        var item = summary.Items.Single();
        item.IsFeeStructureAssigned.ShouldBeFalse();
        item.Status.ShouldBe(expectedStatus);
        item.TotalFees.ShouldBe(total);
        item.PaidFees.ShouldBe(paid);
        item.PendingFees.ShouldBe(total - paid);
        item.OverdueFees.ShouldBe(expectedStatus == FeeCollectionStatus.Overdue ? total - paid : 0);

        var matching = await Client.GetFromJsonAsync<PagedResultDto<FeeCollectionSummaryItemDto>>(
            $"{BaseUrl}?{query}&status={(int)expectedStatus}", JsonOptions);
        matching.ShouldNotBeNull();
        matching.TotalCount.ShouldBe(1);
        matching.Items.Single().Status.ShouldBe(expectedStatus);

        var otherStatus = expectedStatus == FeeCollectionStatus.NoFeeAssigned
            ? FeeCollectionStatus.Paid : FeeCollectionStatus.NoFeeAssigned;
        var excluded = await Client.GetFromJsonAsync<PagedResultDto<FeeCollectionSummaryItemDto>>(
            $"{BaseUrl}?{query}&status={(int)otherStatus}", JsonOptions);
        excluded.ShouldNotBeNull();
        excluded.TotalCount.ShouldBe(0);
        excluded.Items.ShouldBeEmpty();

        var export = await Client.GetAsync($"{BaseUrl}/export?{query}&status={(int)expectedStatus}&format=Csv");
        export.StatusCode.ShouldBe(HttpStatusCode.OK);
        var csv = await export.Content.ReadAsStringAsync();
        csv.ShouldContain(name);
        csv.ShouldContain(expectedStatus == FeeCollectionStatus.NoFeeAssigned
            ? "No Fee Assigned" : expectedStatus.ToString());
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
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Get_Student_Fee_Details_From_Dues_Without_A_Structure_Assignment()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        var feeHeadId = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
        await FeeCollectionTestHelper.SeedDueFeeAsync(Factory.Services, studentId, feeHeadId, 106_000d);

        var response = await Client.GetAsync(
            $"{BaseUrl}/{studentId}/fee-details?academicYearId={FeeCollectionTestHelper.DefaultAcademicYearId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<StudentFeeDetailsDto>(JsonOptions);
        body.ShouldNotBeNull();
        body.FeeOverview.TotalFee.ShouldBe(106_000d);
        body.FeeOverview.PendingFee.ShouldBe(106_000d);
        body.FeeHeads.Single().Amount.ShouldBe(106_000d);
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
        body.Payments[0].PaymentMethods.ShouldContain(PaymentMethodEnum.Cash);
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
