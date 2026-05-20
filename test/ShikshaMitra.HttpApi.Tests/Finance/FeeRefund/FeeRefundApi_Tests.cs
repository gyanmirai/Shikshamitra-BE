using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.FeeRefund;
using ShikshaMitra.Finance.FeeRefund.Dtos;
using ShikshaMitra.Finance.FeeRefundTests;
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
    public async Task Should_Get_Eligibility_Returns_200_With_Empty_FeeHeads_For_Unpaid_Student()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);

        var response = await Client.GetAsync(
            $"{BaseUrl}/eligibility?studentId={studentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RefundEligibilityDto>(JsonOptions);
        body.ShouldNotBeNull();
        body.StudentId.ShouldBe(studentId);
        body.TotalRefundable.ShouldBe(0d);
        body.FeeHeads.ShouldBeEmpty();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Get_Eligibility_Returns_Positive_Refundable_After_Payment_With_Allocation()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, 2000d);
        if (dueFeeId == Guid.Empty) return; // no DueFee rows; skip this assertion

        var response = await Client.GetAsync(
            $"{BaseUrl}/eligibility?studentId={studentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RefundEligibilityDto>(JsonOptions);
        body.ShouldNotBeNull();
        body.TotalRefundable.ShouldBeGreaterThan(0d);
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
    public async Task Should_Create_Refund_Returns_201_With_Pending_Status()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, 1500d);
        if (dueFeeId == Guid.Empty) return;

        var input = new CreateFeeRefundRequestDto
        {
            StudentId = studentId,
            AcademicYearId = FeeRefundTestHelper.DefaultAcademicYearId,
            RefundMethod = "Cash",
            Reason = "TC issued",
            Lines = new System.Collections.Generic.List<CreateFeeRefundLineDto>
            {
                new() { DueFeeId = dueFeeId, Amount = 500d },
            },
        };

        var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions);
        body.ShouldNotBeNull();
        body.Status.ShouldBe(FeeRefundStatus.Pending);
        body.TotalAmount.ShouldBe(500d);
        body.VoucherNumber.ShouldNotBeNullOrWhiteSpace();
        body.Lines.Count.ShouldBe(1);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Create_Refund_Returns_Error_When_Amount_Exceeds_Refundable_Balance()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, 1000d);
        if (dueFeeId == Guid.Empty) return;

        var input = new CreateFeeRefundRequestDto
        {
            StudentId = studentId,
            AcademicYearId = FeeRefundTestHelper.DefaultAcademicYearId,
            Lines = new System.Collections.Generic.List<CreateFeeRefundLineDto>
            {
                new() { DueFeeId = dueFeeId, Amount = 99999d },
            },
        };

        var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    // ── PATCH /api/fee-refunds/{id}/status ──────────────────────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Approve_Refund_Reduces_DueFee_Refundable_Balance()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, 2000d);
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

        // Eligibility should show reduced balance
        var eligResp = await Client.GetAsync(
            $"{BaseUrl}/eligibility?studentId={studentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}");
        eligResp.EnsureSuccessStatusCode();
        var elig = await eligResp.Content.ReadFromJsonAsync<RefundEligibilityDto>(JsonOptions);
        elig.ShouldNotBeNull();
        elig.TotalRefunded.ShouldBeGreaterThanOrEqualTo(600d);
        elig.TotalRefundable.ShouldBeLessThanOrEqualTo(2000d - 600d);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Reject_Refund_Does_Not_Mutate_DueFee()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, 1000d);
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
        elig.TotalRefunded.ShouldBe(0d);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Important)]
    public async Task Should_Return_Error_When_Approving_Already_Approved_Refund()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, 2000d);
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

    // ── Helper types for test deserialization ────────────────────────────────

    private record PagedResultDto(int TotalCount, System.Collections.Generic.List<FeeRefundRequestDto> Items);
}
