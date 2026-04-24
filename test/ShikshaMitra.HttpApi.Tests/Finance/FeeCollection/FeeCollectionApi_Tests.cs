using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.FeeCollection;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.FeeCollections;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class FeeCollectionApi_Tests : ShikshaMitraApiTestBase
{
    private const string BaseUrl = "/api/fee-collections";

    public FeeCollectionApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
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

    // ── GET /api/fee-collections ─────────────────────────────────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Get_Students_Returns_200_With_Correct_Shape()
    {
        var (_, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);

        var url = $"{BaseUrl}?academicYearId={FeeCollectionTestHelper.DefaultAcademicYearId}" +
                  $"&boardId={boardId}" +
                  $"&educationMediumId={FeeCollectionTestHelper.DefaultMediumId}";

        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<StudentFeeCollectionListDto>(JsonOptions);
        body.ShouldNotBeNull();
        body.Items.ShouldNotBeNull();
        body.CurrentPage.ShouldBe(1);
        body.PageSize.ShouldBeGreaterThan(0);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Get_Students_Returns_Empty_Items_When_No_Enrolled_Students()
    {
        var (_, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);

        var url = $"{BaseUrl}?academicYearId={FeeCollectionTestHelper.DefaultAcademicYearId}" +
                  $"&boardId={boardId}" +
                  $"&educationMediumId={FeeCollectionTestHelper.DefaultMediumId}";

        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<StudentFeeCollectionListDto>(JsonOptions);
        body.ShouldNotBeNull();
        // Students created via direct registration are in Registration status, not Enrolled
        body.Items.ShouldNotBeNull();
        body.TotalCount.ShouldBe(0);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Get_Students_With_Search_Returns_200()
    {
        var (_, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);

        var url = $"{BaseUrl}?academicYearId={FeeCollectionTestHelper.DefaultAcademicYearId}" +
                  $"&boardId={boardId}" +
                  $"&educationMediumId={FeeCollectionTestHelper.DefaultMediumId}" +
                  $"&search=Test";

        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── POST /api/fee-collections ────────────────────────────────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Collect_Fee_Returns_201_With_Receipt_Number()
    {
        var (output, _, _) = await FeeCollectionTestHelper.CollectFeeAsync(Client, feeAmount: 1000d);

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

        var input = FeeCollectionTestHelper.BuildCollectFeeInput(
            studentId,
            FeeCollectionTestHelper.DefaultAcademicYearId,
            feeHeadId,
            amount: 500d,
            receivedAmount: 500d);

        var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var output = await response.Content.ReadFromJsonAsync<CollectFeeOutput>(JsonOptions);
        output.ShouldNotBeNull();
        output.Status.ShouldBe(FeeCollectionStatus.Paid);
        output.StudentId.ShouldBe(studentId);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Collect_Fee_Returns_Pending_Status_When_Partial_Amount_Received()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        var feeHeadId = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);

        var input = FeeCollectionTestHelper.BuildCollectFeeInput(
            studentId,
            FeeCollectionTestHelper.DefaultAcademicYearId,
            feeHeadId,
            amount: 1000d,
            receivedAmount: 400d);

        var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var output = await response.Content.ReadFromJsonAsync<CollectFeeOutput>(JsonOptions);
        output.ShouldNotBeNull();
        output.Status.ShouldBe(FeeCollectionStatus.Pending);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Collect_Fee_Returns_Error_For_Invalid_FeeHead()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);

        var input = FeeCollectionTestHelper.BuildCollectFeeInput(
            studentId,
            FeeCollectionTestHelper.DefaultAcademicYearId,
            feeHeadId: Guid.NewGuid(),
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

        var input = new CollectFeeInput
        {
            StudentId = studentId,
            AcademicYearId = FeeCollectionTestHelper.DefaultAcademicYearId,
            TotalAmount = 750d,
            Notes = "UPI payment test",
            SelectedFeeHeads = new System.Collections.Generic.List<SelectedFeeHeadDto>
            {
                new() { FeeHeadId = feeHeadId, Amount = 750d },
            },
            Payments = new System.Collections.Generic.List<FeePaymentDto>
            {
                new()
                {
                    PaymentMethod = PaymentMethodEnum.UPI,
                    Amount = 750d,
                    Details = new System.Collections.Generic.Dictionary<string, object?>
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

    // ── GET /api/fee-collections/{studentId}/fee-details ────────────────────

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
        var (output, studentId, _) = await FeeCollectionTestHelper.CollectFeeAsync(Client, feeAmount: 500d);

        var url = $"{BaseUrl}/{studentId}/fee-details?academicYearId={FeeCollectionTestHelper.DefaultAcademicYearId}";

        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<StudentFeeDetailsDto>(JsonOptions);
        body.ShouldNotBeNull();
        body.Payments.ShouldNotBeNull();
        body.Payments.ShouldNotBeEmpty();
        body.Payments![0].ReceiptNumber.ShouldBe(output.ReceiptNumber);
    }

    // ── GET /api/fee-collections/{studentFeeId:guid} ─────────────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Get_Fee_Collection_Details_Returns_Error_For_Unknown_StudentFeeId()
    {
        var unknownId = Guid.NewGuid();
        var response = await Client.GetAsync($"{BaseUrl}/{unknownId}");

        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    // ── GET /api/fee-collections/{studentId}/comprehensive-fee-details ────────

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Get_Comprehensive_Fee_Details_Returns_500_Not_Implemented()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);

        var url = $"{BaseUrl}/{studentId}/comprehensive-fee-details?academicYearId={FeeCollectionTestHelper.DefaultAcademicYearId}";

        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }
}
