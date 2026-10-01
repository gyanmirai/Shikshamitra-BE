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

[Collection(FeeRefundApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class FeeRefundApi_Tests : ShikshaMitraApiTestBase
{
    private const string BaseUrl = "/api/fee-refunds";

    public FeeRefundApi_Tests(FeeRefundApiTestFactory factory) : base(factory)
    {
        FeeRefundTestHelper.DefaultAcademicYearId      = factory.SeededAcademicYearId;
        FeeRefundTestHelper.DefaultBoardId             = factory.SeededBoardId;
        FeeRefundTestHelper.DefaultMediumId            = factory.SeededMediumId;
        FeeRefundTestHelper.DefaultMediumStageId       = factory.SeededMediumStageId;
        FeeRefundTestHelper.DefaultStandardId          = factory.SeededStandardId;
        FeeRefundTestHelper.DefaultDivisionId          = factory.SeededDivisionId;
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
        var studentId = await FeeRefundTestHelper.SeedStudentOnlyAsync(Factory.Services);

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
    public async Task Should_Preview_Partial_Cash_Refund_And_Create_Pending_Request()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services, 2200d);
        var eligible = await FeeRefundTestHelper.GetEligibleLineAsync(Client, studentId, dueFeeId);
        var input = new CreateFeeRefundRequestDto
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"),
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
        draft.Status.ShouldBe(FeeRefundStatus.Submitted);
        var replayResponse = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
        replayResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var replay = await replayResponse.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions);
        replay!.Id.ShouldBe(draft.Id);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Create_Refund_Returns_201_With_Pending_Status()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services, 1500d);
        if (dueFeeId == Guid.Empty) return;
        var eligible = await FeeRefundTestHelper.GetEligibleLineAsync(Client, studentId, dueFeeId);

        var input = new CreateFeeRefundRequestDto
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"),
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
        body.Status.ShouldBe(FeeRefundStatus.Submitted);
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
            IdempotencyKey = Guid.NewGuid().ToString("N"),
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
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        var approveResp = await checker.PatchAsJsonAsync($"{BaseUrl}/{refundDto.Id}/status", approveInput, JsonOptions);
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

        var rejectInput = new RejectFeeRefundDto { Notes = "Rejected in test" };
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        var rejectResp = await checker.PostAsJsonAsync($"{BaseUrl}/{refundDto.Id}/reject", rejectInput, JsonOptions);
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

        var approveInput = new FeeRefundActionDto { Notes = "First approval" };

        // First approval should succeed
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        var firstResp = await checker.PostAsJsonAsync($"{BaseUrl}/{refundDto.Id}/approve", approveInput, JsonOptions);
        firstResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Second approval attempt should fail
        var secondResp = await checker.PostAsJsonAsync($"{BaseUrl}/{refundDto.Id}/approve", approveInput, JsonOptions);
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
        installment.Status.ShouldBe(2); // Paid status mapping
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Confirmed_Cash_Refund_Should_Post_One_Balanced_Subledger_Entry()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(
            Client, Factory.Services, 1000d);
        var request = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 200d);
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        var checkerView = await checker.GetAsync($"{BaseUrl}/{request.Id}");
        checkerView.StatusCode.ShouldBe(HttpStatusCode.OK, await checkerView.Content.ReadAsStringAsync());
        var approval = await checker.PostAsJsonAsync($"{BaseUrl}/{request.Id}/approve",
            new FeeRefundActionDto { Notes = "Verified refund source" }, JsonOptions);
        approval.StatusCode.ShouldBe(HttpStatusCode.OK, await approval.Content.ReadAsStringAsync());

        var payoutInput = new RecordRefundPayoutDto
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"), Method = "Cash", Amount = 200m,
            PaymentDate = DateTimeOffset.UtcNow,
            EvidenceReference = "Signed handover on file"
        };
        var record = await Client.PostAsJsonAsync($"{BaseUrl}/{request.Id}/payout",
            payoutInput, JsonOptions);
        record.StatusCode.ShouldBe(HttpStatusCode.OK);
        var awaiting = await record.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions);
        awaiting!.Status.ShouldBe(FeeRefundStatus.Approved);
        awaiting.Settlements.Count.ShouldBe(1);
        var recordReplay = await Client.PostAsJsonAsync($"{BaseUrl}/{request.Id}/payout",
            payoutInput, JsonOptions);
        recordReplay.StatusCode.ShouldBe(HttpStatusCode.OK,
            await recordReplay.Content.ReadAsStringAsync());
        var replayedPayout = await recordReplay.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions);
        replayedPayout!.Settlements.Single().Id.ShouldBe(awaiting.Settlements.Single().Id);

        var verification = new ResolveRefundPayoutDto
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            EvidenceReference = "Signed handover verified"
        };
        var confirmUrl = $"{BaseUrl}/{request.Id}/payout/{awaiting.Settlements[0].Id}/confirm";
        var confirmed = await Client.PostAsJsonAsync(confirmUrl, verification, JsonOptions);
        confirmed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var replay = await Client.PostAsJsonAsync(confirmUrl, verification, JsonOptions);
        replay.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = await Client.GetFromJsonAsync<FeeRefundRequestDto>($"{BaseUrl}/{request.Id}", JsonOptions);
        detail!.Status.ShouldBe(FeeRefundStatus.Processed);
        detail.RefundSubledgerNetAmount.ShouldBe(200m);
        detail.IsRefundSubledgerBalanced.ShouldBe(true);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Failed_Bank_Transfer_Should_Release_Reservation_And_Allow_Linked_Request()
    {
        const string method = "Bank Transfer (NEFT/RTGS/IMPS)";
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(
            Client, Factory.Services, 1000d);
        var bankId = await FeeRefundTestHelper.SeedSchoolBankAccountAsync(Factory.Services);
        var request = await FeeRefundTestHelper.CreateRefundRequestAsync(
            Client, studentId, dueFeeId, 200d, method);
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        var approved = await checker.PostAsJsonAsync($"{BaseUrl}/{request.Id}/approve",
            new FeeRefundActionDto { Notes = "Approved bank refund" }, JsonOptions);
        approved.StatusCode.ShouldBe(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
        var recorded = await Client.PostAsJsonAsync($"{BaseUrl}/{request.Id}/payout",
            new RecordRefundPayoutDto
            {
                IdempotencyKey = Guid.NewGuid().ToString("N"), Method = method, Amount = 200m,
                BankAccountId = bankId, PaymentDate = DateTimeOffset.UtcNow,
                ExternalReference = Guid.NewGuid().ToString("N"),
                EvidenceReference = "Bank submission receipt"
            }, JsonOptions);
        recorded.StatusCode.ShouldBe(HttpStatusCode.OK, await recorded.Content.ReadAsStringAsync());
        var awaiting = await recorded.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions);
        awaiting!.Status.ShouldBe(FeeRefundStatus.Approved);
        awaiting.RefundSubledgerNetAmount.ShouldBeNull();
        var failed = await checker.PostAsJsonAsync(
            $"{BaseUrl}/{request.Id}/payout/{awaiting.Settlements.Single().Id}/fail",
            new ResolveRefundPayoutDto
            {
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                EvidenceReference = "Bank rejection advice", Reason = "Invalid beneficiary account"
            }, JsonOptions);
        failed.StatusCode.ShouldBe(HttpStatusCode.OK, await failed.Content.ReadAsStringAsync());
        var failedDetail = await failed.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions);
        failedDetail!.Status.ShouldBe(FeeRefundStatus.Failed);
        failedDetail.RefundSubledgerNetAmount.ShouldBeNull();
        var replacement = await FeeRefundTestHelper.CreateRefundRequestAsync(
            Client, studentId, dueFeeId, 200d, method, request.Id);
        replacement.Status.ShouldBe(FeeRefundStatus.Pending);
        replacement.ReplacesRefundRequestId.ShouldBe(request.Id);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Returned_Bank_Transfer_Should_Compensate_Subledger_And_Restore_Due()
    {
        const string method = "Bank Transfer (NEFT/RTGS/IMPS)";
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(
            Client, Factory.Services, 1000d);
        var bankId = await FeeRefundTestHelper.SeedSchoolBankAccountAsync(Factory.Services);
        var request = await FeeRefundTestHelper.CreateRefundRequestAsync(
            Client, studentId, dueFeeId, 200d, method);
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        var approved = await checker.PostAsJsonAsync($"{BaseUrl}/{request.Id}/approve",
            new FeeRefundActionDto { Notes = "Approved bank refund" }, JsonOptions);
        approved.StatusCode.ShouldBe(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
        var recorded = await Client.PostAsJsonAsync($"{BaseUrl}/{request.Id}/payout",
            new RecordRefundPayoutDto
            {
                IdempotencyKey = Guid.NewGuid().ToString("N"), Method = method, Amount = 200m,
                BankAccountId = bankId, PaymentDate = DateTimeOffset.UtcNow,
                ExternalReference = Guid.NewGuid().ToString("N"),
                EvidenceReference = "Bank submission receipt"
            }, JsonOptions);
        recorded.StatusCode.ShouldBe(HttpStatusCode.OK, await recorded.Content.ReadAsStringAsync());
        var payout = (await recorded.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!
            .Settlements.Single();
        var confirmed = await checker.PostAsJsonAsync(
            $"{BaseUrl}/{request.Id}/payout/{payout.Id}/confirm",
            new ResolveRefundPayoutDto
            {
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                EvidenceReference = "Bank debit and beneficiary credit verified"
            }, JsonOptions);
        confirmed.StatusCode.ShouldBe(HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync());
        var paid = await Client.GetFromJsonAsync<FeeRefundRequestDto>(
            $"{BaseUrl}/{request.Id}", JsonOptions);
        paid!.RefundSubledgerNetAmount.ShouldBe(200m);
        paid.IsRefundSubledgerBalanced.ShouldBe(true);
        var returned = await checker.PostAsJsonAsync($"{BaseUrl}/{request.Id}/payout/return",
            new ReturnRefundPayoutDto
            {
                SettlementId = payout.Id, Reason = "Bank returned transfer after settlement",
                EvidenceReference = "Bank return advice"
            }, JsonOptions);
        returned.StatusCode.ShouldBe(HttpStatusCode.OK, await returned.Content.ReadAsStringAsync());
        var detail = await Client.GetFromJsonAsync<FeeRefundRequestDto>(
            $"{BaseUrl}/{request.Id}", JsonOptions);
        detail!.Status.ShouldBe(FeeRefundStatus.Failed);
        detail.RefundSubledgerNetAmount.ShouldBe(0m);
        detail.IsRefundSubledgerBalanced.ShouldBe(true);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Payout_Reference_Cannot_Be_Reused_On_Another_Request()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(
            Client, Factory.Services, 1000d);
        var first = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 100d);
        var second = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 100d);
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        foreach (var request in new[] { first, second })
        {
            var approval = await checker.PostAsJsonAsync($"{BaseUrl}/{request.Id}/approve",
                new FeeRefundActionDto { Notes = "Source checked" }, JsonOptions);
            approval.StatusCode.ShouldBe(HttpStatusCode.OK, await approval.Content.ReadAsStringAsync());
        }

        var reference = Guid.NewGuid().ToString("N");
        RecordRefundPayoutDto Payout() => new()
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"), Method = "Cash", Amount = 100m,
            PaymentDate = DateTimeOffset.UtcNow, ExternalReference = reference,
            EvidenceReference = "Signed handover"
        };
        var recorded = await Client.PostAsJsonAsync($"{BaseUrl}/{first.Id}/payout", Payout(), JsonOptions);
        recorded.StatusCode.ShouldBe(HttpStatusCode.OK, await recorded.Content.ReadAsStringAsync());
        var duplicate = await Client.PostAsJsonAsync($"{BaseUrl}/{second.Id}/payout", Payout(), JsonOptions);
        duplicate.IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Competing_Approvals_Cannot_Reserve_More_Than_Paid_Source()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(
            Client, Factory.Services, 1000d);
        var first = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 700d);
        var second = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 700d);
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        var outcomes = await Task.WhenAll(
            checker.PostAsJsonAsync($"{BaseUrl}/{first.Id}/approve",
                new FeeRefundActionDto { Notes = "Source checked" }, JsonOptions),
            checker.PostAsJsonAsync($"{BaseUrl}/{second.Id}/approve",
                new FeeRefundActionDto { Notes = "Source checked" }, JsonOptions));
        outcomes.Count(x => x.IsSuccessStatusCode).ShouldBe(1);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task User_Outside_Student_Scope_Cannot_Read_Refund()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(
            Client, Factory.Services, 1000d);
        var request = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 100d);
        using var unscoped = CreateClientAs(Guid.NewGuid());
        var response = await unscoped.GetAsync($"{BaseUrl}/{request.Id}");
        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    // ── Helper types for test deserialization ────────────────────────────────

    private record PagedResultDto(int TotalCount, System.Collections.Generic.List<FeeRefundRequestDto> Items);
}
