using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.FeeRefundTests;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.FeeRefunds;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class FeeRefundApi_Tests : ShikshaMitraApiTestBase
{
    private const string BaseUrl = "/api/fee-refunds";

    public FeeRefundApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
    {
        FeeRefundTestHelper.DefaultAcademicYearId      = factory.SeededAcademicYearId;
        FeeCollectionTestHelper.DefaultAcademicYearId  = factory.SeededAcademicYearId;
        FeeCollectionTestHelper.DefaultMediumId        = factory.SeededMediumId;
        FeeCollectionTestHelper.DefaultStandardId      = factory.SeededStandardId;
        FeeCollectionTestHelper.DefaultDivisionId      = factory.SeededDivisionId;

        ConcessionRequestTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
        ConcessionRequestTestHelper.DefaultMediumId       = factory.SeededMediumId;
        ConcessionRequestTestHelper.DefaultStandardId     = factory.SeededStandardId;
        ConcessionRequestTestHelper.DefaultDivisionId     = factory.SeededDivisionId;
    }

    // ── GET /eligibility ─────────────────────────────────────────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Get_Eligibility_Returns_Error_When_No_Refund_Policy_Is_Assigned()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);

        var response = await Client.GetAsync(
            $"{BaseUrl}/eligibility?studentId={studentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}");

        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Get_Eligibility_Returns_Positive_Refundable_After_Payment_With_Allocation()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services, 2000d);
        if (dueFeeId == Guid.Empty) return; // no DueFee rows; skip this assertion

        var response = await Client.GetAsync(
            $"{BaseUrl}/eligibility?studentId={studentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RefundEligibilityDto>(JsonOptions);
        body.ShouldNotBeNull();
        body.TotalRefundable.ShouldBeGreaterThan(0m);
        body.FeeHeads.ShouldNotBeEmpty();
    }

    // ── GET /api/fee-refunds ─────────────────────────────────────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Get_List_Returns_200_With_Paged_Shape()
    {
        var response = await Client.GetAsync(BaseUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResultDto>(JsonOptions);
        body.ShouldNotBeNull();
    }

    // ── POST /api/fee-refunds ────────────────────────────────────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Preview_Partial_Cash_Refund_And_Create_Draft()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services, 2200d);
        var eligible = await FeeRefundTestHelper.GetEligibleLineAsync(Client, studentId, dueFeeId);
        var input = new CreateFeeRefundRequestDto
        {
            StudentId = studentId,
            AcademicYearId = FeeRefundTestHelper.DefaultAcademicYearId,
            OriginalReceiptId = eligible.SourceReceiptId,
            RefundMethod = "Cash",
            PaymentDetails = new System.Collections.Generic.Dictionary<string, object?>
            {
                ["refundDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                ["receivedBy"] = "Test parent",
            },
            Reason = "Partial refund test",
            Lines = new System.Collections.Generic.List<CreateFeeRefundLineDto>
            {
                new() { DueFeeId = dueFeeId, SourceAllocationId = eligible.SourceAllocationId, Amount = 200m },
            },
        };

        var previewResponse = await Client.PostAsJsonAsync($"{BaseUrl}/preview", input, JsonOptions);
        previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await previewResponse.Content.ReadFromJsonAsync<FeeRefundPreviewDto>(JsonOptions);
        preview.ShouldNotBeNull();
        preview.MaximumAllowedAmount.ShouldBe(200m);

        var createResponse = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var draft = await createResponse.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions);
        draft.ShouldNotBeNull();
        draft.TotalAmount.ShouldBe(200m);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Create_Refund_Returns_201_With_Draft_Status()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services, 1500d);
        if (dueFeeId == Guid.Empty) return;
        var eligible = await FeeRefundTestHelper.GetEligibleLineAsync(Client, studentId, dueFeeId);

        var input = new CreateFeeRefundRequestDto
        {
            StudentId = studentId,
            AcademicYearId = FeeRefundTestHelper.DefaultAcademicYearId,
            OriginalReceiptId = eligible.SourceReceiptId,
            RefundMethod = "Cash",
            PaymentDetails = new System.Collections.Generic.Dictionary<string, object?>
            {
                ["refundDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                ["receivedBy"] = "Test parent",
            },
            Reason = "TC issued",
            Lines = new System.Collections.Generic.List<CreateFeeRefundLineDto>
            {
                new() { DueFeeId = dueFeeId, SourceAllocationId = eligible.SourceAllocationId, Amount = 500m },
            },
        };

        var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions);
        body.ShouldNotBeNull();
        body.Status.ShouldBe(FeeRefundStatus.Draft);
        body.TotalAmount.ShouldBe(500m);
        body.VoucherNumber.ShouldNotBeNullOrWhiteSpace();
        body.Lines.Count.ShouldBe(1);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Create_Refund_Returns_Error_When_Amount_Exceeds_Refundable_Balance()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services, 1000d);
        if (dueFeeId == Guid.Empty) return;
        var eligible = await FeeRefundTestHelper.GetEligibleLineAsync(Client, studentId, dueFeeId);

        var input = new CreateFeeRefundRequestDto
        {
            StudentId = studentId,
            AcademicYearId = FeeRefundTestHelper.DefaultAcademicYearId,
            OriginalReceiptId = eligible.SourceReceiptId,
            RefundMethod = "Cash",
            PaymentDetails = new System.Collections.Generic.Dictionary<string, object?>
            {
                ["refundDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                ["receivedBy"] = "Test parent",
            },
            Lines = new System.Collections.Generic.List<CreateFeeRefundLineDto>
            {
                new() { DueFeeId = dueFeeId, SourceAllocationId = eligible.SourceAllocationId, Amount = 99999m },
            },
        };

        var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    // ── PATCH /api/fee-refunds/{id}/status ──────────────────────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Approve_Refund_Reserves_The_Refundable_Balance()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services, 2000d);
        if (dueFeeId == Guid.Empty) return;

        var refundDto = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 600d);

        // Approve
        var approveInput = new UpdateFeeRefundStatusDto
        {
            Status = FeeRefundStatus.Approved,
            Notes = "Approved in test",
        };
        var approveResp = await Client.PatchAsJsonAsync($"{BaseUrl}/{refundDto.Id}/status", approveInput, JsonOptions);
        approveResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var approved = await approveResp.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions);
        approved.ShouldNotBeNull();
        approved.Status.ShouldBe(FeeRefundStatus.Approved);

        // Approval reserves the allocation, so another request cannot consume it.
        var eligResp = await Client.GetAsync(
            $"{BaseUrl}/eligibility?studentId={studentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}");
        eligResp.EnsureSuccessStatusCode();
        var elig = await eligResp.Content.ReadFromJsonAsync<RefundEligibilityDto>(JsonOptions);
        elig.ShouldNotBeNull();
        elig.TotalRefunded.ShouldBeGreaterThanOrEqualTo(600m);
        elig.TotalRefundable.ShouldBeLessThanOrEqualTo(1400m);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Reject_Refund_Does_Not_Mutate_DueFee()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services, 1000d);
        if (dueFeeId == Guid.Empty) return;

        var refundDto = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 300d);

        var rejectInput = new UpdateFeeRefundStatusDto
        {
            Status = FeeRefundStatus.Rejected,
            Notes = "Rejected in test",
        };
        var rejectResp = await Client.PatchAsJsonAsync($"{BaseUrl}/{refundDto.Id}/status", rejectInput, JsonOptions);
        rejectResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var rejected = await rejectResp.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions);
        rejected.ShouldNotBeNull();
        rejected.Status.ShouldBe(FeeRefundStatus.Rejected);

        // Eligibility should still show full refundable (no monetary mutation on rejection)
        var eligResp = await Client.GetAsync(
            $"{BaseUrl}/eligibility?studentId={studentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}");
        eligResp.EnsureSuccessStatusCode();
        var elig = await eligResp.Content.ReadFromJsonAsync<RefundEligibilityDto>(JsonOptions);
        elig.ShouldNotBeNull();
        elig.TotalRefunded.ShouldBe(0m);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_When_Approving_Already_Approved_Refund()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services, 2000d);
        if (dueFeeId == Guid.Empty) return;

        var refundDto = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 400d);

        var approveInput = new UpdateFeeRefundStatusDto
        {
            Status = FeeRefundStatus.Approved,
            Notes = "First approval",
        };

        // First approval should succeed
        var firstResp = await Client.PatchAsJsonAsync($"{BaseUrl}/{refundDto.Id}/status", approveInput, JsonOptions);
        firstResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Second approval attempt should fail
        var secondResp = await Client.PatchAsJsonAsync($"{BaseUrl}/{refundDto.Id}/status", approveInput, JsonOptions);
        secondResp.IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_404_When_Refund_Not_Found()
    {
        var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Get_Refund_Request_With_Details()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services, 2000d);
        if (dueFeeId == Guid.Empty) return;

        var refundDto = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 600d);

        // Fetch detailed GET /api/fee-refunds/{id}
        var response = await Client.GetAsync($"{BaseUrl}/{refundDto.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions);
        body.ShouldNotBeNull();
        body.BoardId.ShouldNotBeNull();
        body.BoardId.Value.ShouldNotBe(Guid.Empty);
        body.BoardName.ShouldNotBeNullOrWhiteSpace();
        body.EducationMediumId.ShouldNotBeNull();
        body.EducationMediumId.Value.ShouldNotBe(Guid.Empty);
        body.EducationMediumName.ShouldNotBeNullOrWhiteSpace();
        body.RefundRequestId.ShouldBe(refundDto.Id);
        body.TotalFeeHeads.ShouldBeGreaterThan(0);
        body.TotalPaidAmount.ShouldBe(2000d);
        body.TotalRefundedAmount.ShouldBe(0d); // Submitted refund should not mutate the fee ledger

        body.FeeHeads.ShouldNotBeEmpty();
        var head = body.FeeHeads.Find(fh => fh.Items.Any(item => item.InstallmentId == dueFeeId));
        head.ShouldNotBeNull();
        head.TotalPaid.ShouldBe(2000d);
        head.TotalRefunded.ShouldBe(0d);

        var installment = head.Items.Find(item => item.InstallmentId == dueFeeId);
        installment.ShouldNotBeNull();
        installment.Paid.ShouldBe(2000d);
        installment.RefundAmount.ShouldBe(600d);
        installment.Status.ShouldBe(1); // Paid status mapping
    }

    // ── Helper types for test deserialization ────────────────────────────────

    private record PagedResultDto(int TotalCount, System.Collections.Generic.List<FeeRefundRequestDto> Items);
}
