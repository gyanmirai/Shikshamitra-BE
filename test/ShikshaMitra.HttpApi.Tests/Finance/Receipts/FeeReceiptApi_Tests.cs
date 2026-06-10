using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.FeeReceipts;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Receipts.Dtos;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.Finance.FeeReceiptTests;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class FeeReceiptApi_Tests : ShikshaMitraApiTestBase
{
    private const string BaseUrl = "/api/v1/fee-receipts";

    public FeeReceiptApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
    {
        FeeReceiptTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
        FeeReceiptTestHelper.DefaultMediumId       = factory.SeededMediumId;

        ConcessionRequestTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
        ConcessionRequestTestHelper.DefaultMediumId       = factory.SeededMediumId;
        ConcessionRequestTestHelper.DefaultStandardId     = factory.SeededStandardId;
        ConcessionRequestTestHelper.DefaultDivisionId     = factory.SeededDivisionId;
    }

    // ── GET /api/v1/fee-receipts ──────────────────────────────────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Return_200_With_PagedResultDto_When_Board_And_Medium_Provided()
    {
        var (_, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        FeeReceiptTestHelper.DefaultBoardId = boardId;

        var url = $"{BaseUrl}?academicYearId={FeeReceiptTestHelper.DefaultAcademicYearId}" +
                  $"&boardId={boardId}" +
                  $"&educationMediumId={FeeReceiptTestHelper.DefaultMediumId}";

        var response = await Client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResultDto<FeeReceiptListItemDto>>(JsonOptions);
        body.ShouldNotBeNull();
        body.TotalCount.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_400_When_BoardId_Missing()
    {
        var url = $"{BaseUrl}?academicYearId={FeeReceiptTestHelper.DefaultAcademicYearId}" +
                  $"&educationMediumId={FeeReceiptTestHelper.DefaultMediumId}";

        var response = await Client.GetAsync(url);

        // BoardId defaults to Guid.Empty which fails server-side validation
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Filtered_Receipts_By_ReceiptKind()
    {
        var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        FeeReceiptTestHelper.DefaultBoardId = boardId;

        await FeeReceiptTestHelper.SeedReceiptAsync(Factory.Services, studentId, FeeReceiptKind.FinalFee);
        await FeeReceiptTestHelper.SeedReceiptAsync(Factory.Services, studentId, FeeReceiptKind.ProvisionalFee);
        await FeeReceiptTestHelper.SeedReceiptAsync(Factory.Services, studentId, FeeReceiptKind.ProvisionalCharge, FeeReceiptScope.Charge);

        var url = $"{BaseUrl}?academicYearId={FeeReceiptTestHelper.DefaultAcademicYearId}" +
                  $"&boardId={boardId}" +
                  $"&educationMediumId={FeeReceiptTestHelper.DefaultMediumId}" +
                  $"&receiptKind={(int)FeeReceiptKind.FinalFee}";

        var body = await GetAndReadAsync<PagedResultDto<FeeReceiptListItemDto>>(url);

        body.Items.ShouldAllBe(r => r.ReceiptKind == FeeReceiptKind.FinalFee);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Receipts_With_Kind_Label()
    {
        var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        FeeReceiptTestHelper.DefaultBoardId = boardId;

        await FeeReceiptTestHelper.SeedReceiptAsync(Factory.Services, studentId, FeeReceiptKind.ProvisionalCharge, FeeReceiptScope.Charge);

        var url = $"{BaseUrl}?academicYearId={FeeReceiptTestHelper.DefaultAcademicYearId}" +
                  $"&boardId={boardId}" +
                  $"&educationMediumId={FeeReceiptTestHelper.DefaultMediumId}" +
                  $"&receiptKind={(int)FeeReceiptKind.ProvisionalCharge}";

        var body = await GetAndReadAsync<PagedResultDto<FeeReceiptListItemDto>>(url);

        body.Items.ShouldAllBe(r => r.ReceiptKindLabel == "Kachi Receipt (Charge)");
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Two_Rows_For_Same_Transaction_With_Fee_And_Charge_Lines()
    {
        var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        FeeReceiptTestHelper.DefaultBoardId = boardId;

        var sharedTxnId = Guid.NewGuid();
        await FeeReceiptTestHelper.SeedReceiptAsync(Factory.Services, studentId, FeeReceiptKind.FinalFee, FeeReceiptScope.Fee, 1000d, sharedTxnId);
        await FeeReceiptTestHelper.SeedReceiptAsync(Factory.Services, studentId, FeeReceiptKind.ProvisionalCharge, FeeReceiptScope.Charge, 200d, sharedTxnId);

        var url = $"{BaseUrl}?academicYearId={FeeReceiptTestHelper.DefaultAcademicYearId}" +
                  $"&boardId={boardId}" +
                  $"&educationMediumId={FeeReceiptTestHelper.DefaultMediumId}";

        var body = await GetAndReadAsync<PagedResultDto<FeeReceiptListItemDto>>(url);

        var fromThisTxn = body.Items.Where(r => r.FeeCollectionTransactionId == sharedTxnId).ToList();
        fromThisTxn.Count.ShouldBe(2);
        fromThisTxn.ShouldContain(r => r.ReceiptKind == FeeReceiptKind.FinalFee);
        fromThisTxn.ShouldContain(r => r.ReceiptKind == FeeReceiptKind.ProvisionalCharge);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Filter_By_Month_Returning_Only_Receipts_In_That_Month()
    {
        var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        FeeReceiptTestHelper.DefaultBoardId = boardId;

        await FeeReceiptTestHelper.SeedReceiptAsync(Factory.Services, studentId);

        var currentMonth = DateTime.UtcNow.Month;
        var nextMonth    = currentMonth == 12 ? 1 : currentMonth + 1;

        var urlCurrentMonth = $"{BaseUrl}?academicYearId={FeeReceiptTestHelper.DefaultAcademicYearId}" +
                              $"&boardId={boardId}&educationMediumId={FeeReceiptTestHelper.DefaultMediumId}" +
                              $"&month={currentMonth}";

        var urlNextMonth = $"{BaseUrl}?academicYearId={FeeReceiptTestHelper.DefaultAcademicYearId}" +
                           $"&boardId={boardId}&educationMediumId={FeeReceiptTestHelper.DefaultMediumId}" +
                           $"&month={nextMonth}";

        var bodyCurrentMonth = await GetAndReadAsync<PagedResultDto<FeeReceiptListItemDto>>(urlCurrentMonth);
        var bodyNextMonth    = await GetAndReadAsync<PagedResultDto<FeeReceiptListItemDto>>(urlNextMonth);

        bodyCurrentMonth.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
        bodyNextMonth.Items.ShouldAllBe(r => r.ReceiptDate.Month == nextMonth);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Paginated_Results()
    {
        var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        FeeReceiptTestHelper.DefaultBoardId = boardId;

        for (int i = 0; i < 3; i++)
            await FeeReceiptTestHelper.SeedReceiptAsync(Factory.Services, studentId);

        var url = $"{BaseUrl}?academicYearId={FeeReceiptTestHelper.DefaultAcademicYearId}" +
                  $"&boardId={boardId}&educationMediumId={FeeReceiptTestHelper.DefaultMediumId}" +
                  $"&maxResultCount=2&skipCount=0";

        var body = await GetAndReadAsync<PagedResultDto<FeeReceiptListItemDto>>(url);

        body.Items.Count.ShouldBeLessThanOrEqualTo(2);
    }

    // ── GET /api/v1/fee-receipts/{id} ─────────────────────────────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Return_Receipt_Detail_With_Kind_Label()
    {
        var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        FeeReceiptTestHelper.DefaultBoardId = boardId;

        var receipt = await FeeReceiptTestHelper.SeedReceiptAsync(Factory.Services, studentId, FeeReceiptKind.ProvisionalFee);

        var detail = await GetAndReadAsync<FeeReceiptDetailDto>($"{BaseUrl}/{receipt.Id}");

        detail.Id.ShouldBe(receipt.Id);
        detail.ReceiptKind.ShouldBe(FeeReceiptKind.ProvisionalFee);
        detail.ReceiptKindLabel.ShouldBe("Kachi Receipt (Fee)");
        detail.VerificationStatus.ShouldBe(FeeReceiptVerificationStatus.PendingVerification);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_404_For_Unknown_Receipt_Id()
    {
        var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── GET /api/v1/fee-receipts/{id}/pdf ─────────────────────────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Generate_And_Return_Pdf_For_FinalFee_Receipt()
    {
        var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        FeeReceiptTestHelper.DefaultBoardId = boardId;

        var receipt = await FeeReceiptTestHelper.SeedReceiptAsync(Factory.Services, studentId, FeeReceiptKind.FinalFee);

        var response = await Client.GetAsync($"{BaseUrl}/{receipt.Id}/pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Generate_And_Return_Pdf_For_ProvisionalFee_Receipt()
    {
        var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        FeeReceiptTestHelper.DefaultBoardId = boardId;

        var receipt = await FeeReceiptTestHelper.SeedReceiptAsync(Factory.Services, studentId, FeeReceiptKind.ProvisionalFee);

        var response = await Client.GetAsync($"{BaseUrl}/{receipt.Id}/pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Generate_And_Return_Pdf_For_ProvisionalCharge_Receipt()
    {
        var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        FeeReceiptTestHelper.DefaultBoardId = boardId;

        var receipt = await FeeReceiptTestHelper.SeedReceiptAsync(
            Factory.Services, studentId, FeeReceiptKind.ProvisionalCharge, FeeReceiptScope.Charge);

        var response = await Client.GetAsync($"{BaseUrl}/{receipt.Id}/pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Extended)]
    public async Task Should_Cache_Pdf_After_First_Request()
    {
        var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        FeeReceiptTestHelper.DefaultBoardId = boardId;

        var receipt = await FeeReceiptTestHelper.SeedReceiptAsync(Factory.Services, studentId);

        // First request: generates lazily
        var first  = await Client.GetAsync($"{BaseUrl}/{receipt.Id}/pdf");
        // Second request: should be served from blob
        var second = await Client.GetAsync($"{BaseUrl}/{receipt.Id}/pdf");

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify detail now shows HasPdf = true
        var detail = await GetAndReadAsync<FeeReceiptDetailDto>($"{BaseUrl}/{receipt.Id}");
        detail.HasPdf.ShouldBeTrue();
    }

    // ── Payment policy unit-level stub test ───────────────────────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Receipt_With_Verification_Status_PendingVerification_For_ProvisionalFee()
    {
        var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
        FeeReceiptTestHelper.DefaultBoardId = boardId;

        var receipt = await FeeReceiptTestHelper.SeedReceiptAsync(
            Factory.Services, studentId, FeeReceiptKind.ProvisionalFee);

        var detail = await GetAndReadAsync<FeeReceiptDetailDto>($"{BaseUrl}/{receipt.Id}");

        detail.VerificationStatus.ShouldBe(FeeReceiptVerificationStatus.PendingVerification);
        detail.LinkedFinalReceiptId.ShouldBeNull();
    }
}
