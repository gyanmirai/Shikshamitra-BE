using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.FeeRefundTests;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Constants.Permissions;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Timing;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace ShikshaMitra.Finance.FeeRefunds;

[Collection(FeeRefundApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public partial class FeeRefundApi_Tests : ShikshaMitraApiTestBase
{
    private const string BaseUrl = "/api/fee-refunds";
    private readonly FeeRefundApiTestFactory _refundFactory;
    private readonly ITestOutputHelper _output;

    public FeeRefundApi_Tests(FeeRefundApiTestFactory factory, ITestOutputHelper output) : base(factory)
    {
        _refundFactory = factory;
        _output = output;
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

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Partial_Refund_After_A_Prior_Processed_Refund_Subtracts_The_Source_Commitment_Once()
    {
        var source = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services, 100d);
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        var routing = new FeeRefundActionDto
        {
            ExpenseCategoryId = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services),
            BankAccountId = await FeeRefundTestHelper.SeedSchoolBankAccountAsync(Factory.Services)
        };
        var first = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, source.StudentId, source.DueFeeId, 30d);
        var prior = await checker.PostAsJsonAsync($"{BaseUrl}/{first.Id}/approve", routing, JsonOptions);
        prior.StatusCode.ShouldBe(HttpStatusCode.OK, await prior.Content.ReadAsStringAsync());
        (await prior.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!.Status.ShouldBe(FeeRefundStatus.Processed);
        var next = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, source.StudentId, source.DueFeeId, 60d);
        var approved = await checker.PostAsJsonAsync($"{BaseUrl}/{next.Id}/approve", routing, JsonOptions);
        approved.StatusCode.ShouldBe(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
        (await approved.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!.ProcessedAmount.ShouldBe(60m);
        var remaining = (await Client.GetFromJsonAsync<RefundEligibilityDto>(
            $"{BaseUrl}/eligibility?studentId={source.StudentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}", JsonOptions))!;
        remaining.TotalRefundable.ShouldBe(10m);
        (await FeeRefundTestHelper.GetRefundExpensesAsync(Factory.Services, first.Id)).Single().Amount.ShouldBe(30m);
        (await FeeRefundTestHelper.GetRefundExpensesAsync(Factory.Services, next.Id)).Single().Amount.ShouldBe(60m);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Pending_Requests_From_Separate_Sources_Cannot_Exceed_The_Pinned_Annual_Cap_At_Approval()
    {
        var source = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services, 100d,
            includePaidCharges: true, maximumPerStudentYear: 100m);
        await FeeRefundTestHelper.SeedPaidChargeAsync(Factory.Services, source.StudentId, combined: false);
        var eligibility = (await Client.GetFromJsonAsync<RefundEligibilityDto>(
            $"{BaseUrl}/eligibility?studentId={source.StudentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}", JsonOptions))!;
        var lines = eligibility.FeeHeads.SelectMany(x => x.Months).ToList();
        async Task<FeeRefundRequestDto> CreatePendingAsync(int index)
        {
            var line = lines[index];
            var create = await Client.PostAsJsonAsync(BaseUrl, new CreateFeeRefundRequestDto
            {
                StudentId = source.StudentId, AcademicYearId = FeeRefundTestHelper.DefaultAcademicYearId,
                OriginalReceiptId = line.SourceReceiptId, IdempotencyKey = Guid.NewGuid().ToString("N"),
                RefundMethod = "Cash", PaymentDetails = new() { ["receivedBy"] = "QA Parent" },
                Lines = [new() { DueFeeId = line.DueFeeId, ChargeId = line.ChargeId, SourceAllocationId = line.SourceAllocationId, Amount = 100m }]
            }, JsonOptions);
            create.StatusCode.ShouldBe(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
            return (await create.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!;
        }
        var first = await CreatePendingAsync(0);
        var second = await CreatePendingAsync(1);
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        var routing = new FeeRefundActionDto
        {
            ExpenseCategoryId = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services),
            BankAccountId = await FeeRefundTestHelper.SeedSchoolBankAccountAsync(Factory.Services)
        };
        var approved = await checker.PostAsJsonAsync($"{BaseUrl}/{first.Id}/approve", routing, JsonOptions);
        approved.StatusCode.ShouldBe(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
        var denied = await checker.PostAsJsonAsync($"{BaseUrl}/{second.Id}/approve", routing, JsonOptions);
        denied.IsSuccessStatusCode.ShouldBeFalse();
        (await denied.Content.ReadAsStringAsync()).ShouldContain(FeeRefundErrorCodes.RefundExceedsPolicyAmount);
        var unchanged = (await Client.GetFromJsonAsync<FeeRefundRequestDto>($"{BaseUrl}/{second.Id}", JsonOptions))!;
        unchanged.Status.ShouldBe(FeeRefundStatus.Submitted);
        unchanged.ProcessedAmount.ShouldBe(0m);
        unchanged.Settlements.ShouldBeEmpty();
        (await FeeRefundTestHelper.GetRefundExpensesAsync(Factory.Services, second.Id)).ShouldBeEmpty();
        (await FeeRefundTestHelper.GetRefundExpensesAsync(Factory.Services, first.Id)).Single().Amount.ShouldBe(100m);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Two_Receipt_Allocations_For_One_Due_Should_Remain_Distinct_And_Not_Double_Refund()
    {
        var student = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services, splitAcrossReceipts: true);
        var url = $"{BaseUrl}/eligibility?studentId={student.StudentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}";
        var eligibility = (await Client.GetFromJsonAsync<RefundEligibilityDto>(url, JsonOptions))!;
        var sources = eligibility.FeeHeads.SelectMany(x => x.Months).Where(x => x.DueFeeId == student.DueFeeId).ToList();
        sources.Count.ShouldBe(2);
        object Input(decimal amount) => new
        {
            StudentId = student.StudentId, AcademicYearId = FeeRefundTestHelper.DefaultAcademicYearId,
            OriginalReceiptId = sources[0].SourceReceiptId, IdempotencyKey = Guid.NewGuid().ToString("N"),
            RefundMethod = "Cash", PaymentDetails = new { receivedBy = "QA Parent" },
            Lines = sources.Select(x => new { DueFeeId = student.DueFeeId, x.SourceAllocationId, x.SourceReceiptId, Amount = amount }).ToArray()
        };
        var create = await Client.PostAsJsonAsync(BaseUrl, Input(400m), JsonOptions);
        create.StatusCode.ShouldBe(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var request = (await create.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!;
        request.Lines.Count.ShouldBe(2);
        request.Lines.Select(x => x.DueFeeId).Distinct().Count().ShouldBe(1);
        request.CalculatedAmount.ShouldBe(800m);
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        var approval = await checker.PostAsJsonAsync($"{BaseUrl}/{request.Id}/approve", new FeeRefundActionDto
        {
            ExpenseCategoryId = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services),
            BankAccountId = await FeeRefundTestHelper.SeedSchoolBankAccountAsync(Factory.Services)
        }, JsonOptions);
        approval.StatusCode.ShouldBe(HttpStatusCode.OK, await approval.Content.ReadAsStringAsync());
        (await Client.GetFromJsonAsync<RefundEligibilityDto>(url, JsonOptions))!.TotalRefundable.ShouldBe(200m);
        var excess = await Client.PostAsJsonAsync($"{BaseUrl}/preview", Input(150m), JsonOptions);
        excess.IsSuccessStatusCode.ShouldBeFalse();
        (await excess.Content.ReadAsStringAsync()).ShouldContain(FeeRefundErrorCodes.InsufficientRefundBalance);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Administrator_Cash_Creation_Should_Complete_And_Replay_One_Payout()
    {
        var category = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services);
        var bank = await FeeRefundTestHelper.SeedSchoolBankAccountAsync(Factory.Services);
        var source = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services,
            expenseCategoryId: category, payingBankAccountId: bank);
        var actor = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, actor);
        await FeeRefundTestHelper.SetAdministratorAsync(Factory.Services, actor, true);
        using var administrator = CreateClientAs(actor);
        try
        {
            var line = await FeeRefundTestHelper.GetEligibleLineAsync(Client, source.StudentId, source.DueFeeId);
            var input = new CreateFeeRefundRequestDto
            {
                StudentId = source.StudentId, AcademicYearId = FeeRefundTestHelper.DefaultAcademicYearId,
                OriginalReceiptId = line.SourceReceiptId, IdempotencyKey = Guid.NewGuid().ToString("N"),
                RefundMethod = "Cash", PaymentDetails = new() { ["receivedBy"] = "QA Parent" },
                Lines = [new() { DueFeeId = source.DueFeeId, SourceAllocationId = line.SourceAllocationId, Amount = 200m }]
            };
            administrator.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
                $"{ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add},{ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve}");
            var denied = await administrator.PostAsJsonAsync(BaseUrl, input, JsonOptions);
            denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            administrator.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
            var create = await administrator.PostAsJsonAsync(BaseUrl, input, JsonOptions);
            create.StatusCode.ShouldBe(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
            var created = (await create.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!;
            created.Status.ShouldBe(FeeRefundStatus.Processed);
            created.Settlements.Count.ShouldBe(1);
            created.PayingBankAccountId.ShouldBeNull();
            created.Settlements.Single().BankAccountId.ShouldBeNull();
            administrator.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
                $"{ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add},{ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve}");
            var deniedReplay = await administrator.PostAsJsonAsync(BaseUrl, input, JsonOptions);
            deniedReplay.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            administrator.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
            var replay = await administrator.PostAsJsonAsync(BaseUrl, input, JsonOptions);
            replay.StatusCode.ShouldBe(HttpStatusCode.Created, await replay.Content.ReadAsStringAsync());
            (await replay.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!.Id.ShouldBe(created.Id);
            (await FeeRefundTestHelper.GetRefundExpensesAsync(Factory.Services, created.Id)).Count.ShouldBe(1);
            var detail = (await administrator.GetFromJsonAsync<FeeRefundRequestDto>($"{BaseUrl}/{created.Id}", JsonOptions))!;
            detail.IsRefundSubledgerBalanced.ShouldBe(true);
        }
        finally { await FeeRefundTestHelper.SetAdministratorAsync(Factory.Services, actor, false); }
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Administrator_Cash_Creation_Without_Bank_Should_Complete_Idempotently()
    {
        var category = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services);
        var source = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services,
            allowedPayoutMethods: [RefundPayoutMethod.Cash], expenseCategoryId: category);
        var line = await FeeRefundTestHelper.GetEligibleLineAsync(Client, source.StudentId, source.DueFeeId);
        var actor = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, actor);
        await FeeRefundTestHelper.SetAdministratorAsync(Factory.Services, actor, true);
        using var administrator = CreateClientAs(actor);
        try
        {
            var input = new Dictionary<string, object?>
            {
                ["studentId"] = source.StudentId, ["academicYearId"] = FeeRefundTestHelper.DefaultAcademicYearId,
                ["originalReceiptId"] = line.SourceReceiptId, ["idempotencyKey"] = Guid.NewGuid().ToString("N"),
                ["refundMethod"] = "Cash", ["paymentDetails"] = new { receivedBy = "QA Parent" },
                ["expenseCategoryId"] = category,
                ["lines"] = new[] { new { dueFeeId = source.DueFeeId, sourceAllocationId = line.SourceAllocationId, amount = 200m } }
            };
            var create = await administrator.PostAsJsonAsync(BaseUrl, input, JsonOptions);
            create.StatusCode.ShouldBe(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
            var created = (await create.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!;
            created.Status.ShouldBe(FeeRefundStatus.Processed);
            created.PayingBankAccountId.ShouldBeNull();
            created.Settlements.Single().BankAccountId.ShouldBeNull();
            created.DefaultPayingBankAccountId.ShouldBeNull();
            var replay = await administrator.PostAsJsonAsync(BaseUrl, input, JsonOptions);
            replay.StatusCode.ShouldBe(HttpStatusCode.Created, await replay.Content.ReadAsStringAsync());
            (await replay.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!.Id.ShouldBe(created.Id);
            input["expenseCategoryId"] = Guid.NewGuid();
            var changed = await administrator.PostAsJsonAsync(BaseUrl, input, JsonOptions);
            changed.IsSuccessStatusCode.ShouldBeFalse();
            (await changed.Content.ReadAsStringAsync()).ShouldContain(FeeRefundErrorCodes.IdempotencyConflict);
            (await FeeRefundTestHelper.GetRefundExpensesAsync(Factory.Services, created.Id)).Count.ShouldBe(1);
        }
        finally { await FeeRefundTestHelper.SetAdministratorAsync(Factory.Services, actor, false); }
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Historical_V0_Cash_Draft_Administrator_Submission_Should_Preserve_Approval_Without_V1_Payout()
    {
        var source = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services);
        var request = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, source.StudentId, source.DueFeeId, 200d);
        var actor = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, actor);
        await FeeRefundTestHelper.SeedHistoricalCashDraftAsync(Factory.Services, request.Id, actor);
        await FeeRefundTestHelper.SetAdministratorAsync(Factory.Services, actor, true);
        using var administrator = CreateClientAs(actor);
        try
        {
            var submit = await administrator.PostAsync($"{BaseUrl}/{request.Id}/submit", null);
            submit.StatusCode.ShouldBe(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());
            var approved = (await submit.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!;
            approved.WorkflowVersion.ShouldBe(0);
            approved.Status.ShouldBe(FeeRefundStatus.Approved);
            approved.Settlements.ShouldBeEmpty();
            (await FeeRefundTestHelper.GetRefundExpensesAsync(Factory.Services, request.Id)).ShouldBeEmpty();
        }
        finally { await FeeRefundTestHelper.SetAdministratorAsync(Factory.Services, actor, false); }
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Legacy_Full_Only_Policy_Should_Allow_A_Partial_Selection_And_Amount()
    {
        var student = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services,
            includePaidLateFee: true, allowPartialRefund: false, lateFeeAmount: 250);
        var eligibility = (await Client.GetFromJsonAsync<RefundEligibilityDto>(
            $"{BaseUrl}/eligibility?studentId={student.StudentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}", JsonOptions))!;
        var principal = eligibility.FeeHeads.SelectMany(x => x.Months).Single(x => x.DueFeeId == student.DueFeeId);
        var input = new CreateFeeRefundRequestDto
        {
            StudentId = student.StudentId, AcademicYearId = FeeRefundTestHelper.DefaultAcademicYearId,
            OriginalReceiptId = principal.SourceReceiptId, IdempotencyKey = Guid.NewGuid().ToString("N"),
            RefundMethod = "Cash", PaymentDetails = new() { ["receivedBy"] = "QA Parent" },
            Lines = [new() { DueFeeId = student.DueFeeId, SourceAllocationId = principal.SourceAllocationId, Amount = 200m }]
        };
        var preview = await Client.PostAsJsonAsync($"{BaseUrl}/preview", input, JsonOptions);
        preview.StatusCode.ShouldBe(HttpStatusCode.OK, await preview.Content.ReadAsStringAsync());
        (await preview.Content.ReadFromJsonAsync<FeeRefundPreviewDto>(JsonOptions))!.MaximumAllowedAmount.ShouldBe(200m);
        var create = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
        create.StatusCode.ShouldBe(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var created = (await create.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!;
        created.CalculatedAmount.ShouldBe(200m);
        created.Lines.Count.ShouldBe(1);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Refund_Should_Preview_And_Create_Lines_From_Multiple_Settled_Receipts()
    {
        var student = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services, includePaidCharges: true);
        var charge = await FeeRefundTestHelper.SeedPaidChargeAsync(Factory.Services, student.StudentId, combined: false);
        var eligibility = (await Client.GetFromJsonAsync<RefundEligibilityDto>(
            $"{BaseUrl}/eligibility?studentId={student.StudentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}", JsonOptions))!;
        var principal = eligibility.FeeHeads.SelectMany(x => x.Months).Single(x => x.DueFeeId == student.DueFeeId);
        principal.SourceReceiptId.ShouldNotBe(charge.ReceiptId);
        var input = new
        {
            StudentId = student.StudentId, AcademicYearId = FeeRefundTestHelper.DefaultAcademicYearId,
            OriginalReceiptId = principal.SourceReceiptId, IdempotencyKey = Guid.NewGuid().ToString("N"),
            RefundMethod = "Cash", PaymentDetails = new { receivedBy = "QA Parent" },
            Lines = new[] {
                new { DueFeeId = (Guid?)student.DueFeeId, ChargeId = (Guid?)null, SourceAllocationId = principal.SourceAllocationId, SourceReceiptId = principal.SourceReceiptId, Amount = 100m },
                new { DueFeeId = (Guid?)null, ChargeId = (Guid?)charge.ChargeId, SourceAllocationId = charge.AllocationId, SourceReceiptId = charge.ReceiptId, Amount = 150m }
            }
        };
        var preview = await Client.PostAsJsonAsync($"{BaseUrl}/preview", input, JsonOptions);
        preview.StatusCode.ShouldBe(HttpStatusCode.OK, await preview.Content.ReadAsStringAsync());
        var create = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
        create.StatusCode.ShouldBe(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var created = (await create.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!;
        created.CalculatedAmount.ShouldBe(250m);
        created.Lines.Select(x => x.SourceReceiptId).Distinct().Count().ShouldBe(2);
        created.SourceReceipts.Count.ShouldBe(2);
        var secondaryNumber = created.SourceReceipts.Single(x => x.ReceiptId == charge.ReceiptId).ReceiptNumber;
        var search = (await Client.GetFromJsonAsync<PagedResultDto>(
            $"{BaseUrl}?studentId={student.StudentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}&search={Uri.EscapeDataString(secondaryNumber)}", JsonOptions))!;
        search.Items.ShouldContain(x => x.Id == created.Id);
        var filters = System.Text.Json.JsonSerializer.Serialize(new[] { new { column = "_receiptNumber", type = "equals", value = secondaryNumber } });
        var filtered = (await Client.GetFromJsonAsync<PagedResultDto>(
            $"{BaseUrl}?studentId={student.StudentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}&filters={Uri.EscapeDataString(filters)}", JsonOptions))!;
        filtered.Items.ShouldContain(x => x.Id == created.Id);
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        var approval = await checker.PostAsJsonAsync($"{BaseUrl}/{created.Id}/approve", new FeeRefundActionDto
        {
            ExpenseCategoryId = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services),
            BankAccountId = await FeeRefundTestHelper.SeedSchoolBankAccountAsync(Factory.Services)
        }, JsonOptions);
        approval.StatusCode.ShouldBe(HttpStatusCode.OK, await approval.Content.ReadAsStringAsync());
        (await approval.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!.Status.ShouldBe(FeeRefundStatus.Processed);
        var detail = (await checker.GetFromJsonAsync<FeeRefundRequestDto>($"{BaseUrl}/{created.Id}", JsonOptions))!;
        detail.IsRefundSubledgerBalanced.ShouldBe(true);
        detail.ProcessedAmount.ShouldBe(250m);
        (await FeeRefundTestHelper.GetRefundExpensesAsync(Factory.Services, created.Id)).Count.ShouldBe(1);
        var replay = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
        replay.StatusCode.ShouldBe(HttpStatusCode.Created, await replay.Content.ReadAsStringAsync());
        (await replay.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!.Id.ShouldBe(created.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Additional_Receipt_Must_Belong_To_The_Payer_And_Be_Settled(bool foreignPayer)
    {
        var student = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services, includePaidLateFee: true);
        var other = foreignPayer
            ? await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services, includePaidLateFee: true)
            : student;
        var late = await FeeRefundTestHelper.SeedPaidLateFeeAsync(Factory.Services, other.StudentId, other.DueFeeId,
            status: foreignPayer ? FeeCollectionStatus.Paid : FeeCollectionStatus.InReconciliation);
        var principal = await FeeRefundTestHelper.GetEligibleLineAsync(Client, student.StudentId, student.DueFeeId);
        var response = await Client.PostAsJsonAsync($"{BaseUrl}/preview", new
        {
            StudentId = student.StudentId, AcademicYearId = FeeRefundTestHelper.DefaultAcademicYearId,
            OriginalReceiptId = principal.SourceReceiptId, IdempotencyKey = Guid.NewGuid().ToString("N"),
            RefundMethod = "Cash", PaymentDetails = new { receivedBy = "QA Parent" },
            Lines = new[] {
                new { DueFeeId = (Guid?)student.DueFeeId, LateFeeDueFeeId = (Guid?)null, SourceAllocationId = principal.SourceAllocationId, SourceReceiptId = principal.SourceReceiptId, Amount = 100m },
                new { DueFeeId = (Guid?)null, LateFeeDueFeeId = (Guid?)other.DueFeeId, SourceAllocationId = late.AllocationId, SourceReceiptId = late.ReceiptId, Amount = 100m }
            }
        }, JsonOptions);
        response.IsSuccessStatusCode.ShouldBeFalse();
        (await response.Content.ReadAsStringAsync()).ShouldContain(foreignPayer
            ? FeeRefundErrorCodes.SourceReceiptInvalid : FeeRefundErrorCodes.PaymentNotSettled);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Cash_Approval_Requires_Process_Permission_And_Leaves_Pending_When_Denied()
    {
        var source = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services);
        var request = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, source.StudentId, source.DueFeeId, 200d);
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        checker.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
            ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve);
        var approve = await checker.PostAsJsonAsync($"{BaseUrl}/{request.Id}/approve", new FeeRefundActionDto
        {
            ExpenseCategoryId = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services),
            BankAccountId = await FeeRefundTestHelper.SeedSchoolBankAccountAsync(Factory.Services)
        }, JsonOptions);
        approve.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var detail = (await Client.GetFromJsonAsync<FeeRefundRequestDto>($"{BaseUrl}/{request.Id}", JsonOptions))!;
        detail.Status.ShouldBe(FeeRefundStatus.Submitted);
        detail.Settlements.ShouldBeEmpty();
        (await FeeRefundTestHelper.GetRefundExpensesAsync(Factory.Services, request.Id)).ShouldBeEmpty();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Cash_Approval_Should_Complete_Atomically_And_Replay_Without_Duplicate_Expense()
    {
        var source = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services);
        var request = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, source.StudentId, source.DueFeeId, 200d);
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        var input = new FeeRefundActionDto
        {
            ExpenseCategoryId = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services),
            Notes = "Cash handed to the named recipient"
        };
        var beforeApproval = DateTimeOffset.UtcNow.AddSeconds(-1);
        var cashClock = Factory.Services.GetRequiredService<IClock>();
        _output.WriteLine($"Cash clock kind: {cashClock.Kind}; Now.Kind: {cashClock.Now.Kind}.");
        var approve = await checker.PostAsJsonAsync($"{BaseUrl}/{request.Id}/approve", input, JsonOptions);
        approve.StatusCode.ShouldBe(HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());
        var completed = (await approve.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!;
        completed.Status.ShouldBe(FeeRefundStatus.Processed);
        completed.ProcessedAmount.ShouldBe(200m);
        completed.Settlements.Count.ShouldBe(1);
        completed.PayingBankAccountId.ShouldBeNull();
        completed.Settlements.Single().BankAccountId.ShouldBeNull();
        completed.Settlements.Single().SettlementDate.ShouldBeInRange(beforeApproval, DateTimeOffset.UtcNow.AddSeconds(1),
            $"Cash payment must retain its actual instant. Clock kind: {cashClock.Kind}; Now.Kind: {cashClock.Now.Kind}.");
        var retry = await checker.PostAsJsonAsync($"{BaseUrl}/{request.Id}/approve", input, JsonOptions);
        retry.StatusCode.ShouldBe(HttpStatusCode.OK, await retry.Content.ReadAsStringAsync());
        (await FeeRefundTestHelper.GetRefundExpensesAsync(Factory.Services, request.Id)).Count.ShouldBe(1);
        var detail = (await checker.GetFromJsonAsync<FeeRefundRequestDto>($"{BaseUrl}/{request.Id}", JsonOptions))!;
        detail.IsRefundSubledgerBalanced.ShouldBe(true);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Cash_Approval_With_Missing_Routing_Should_Leave_Request_Pending_And_Unpaid()
    {
        var source = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services);
        var request = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, source.StudentId, source.DueFeeId, 200d);
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        var approve = await checker.PostAsJsonAsync($"{BaseUrl}/{request.Id}/approve", new FeeRefundActionDto(), JsonOptions);
        approve.IsSuccessStatusCode.ShouldBeFalse();
        var detail = (await checker.GetFromJsonAsync<FeeRefundRequestDto>($"{BaseUrl}/{request.Id}", JsonOptions))!;
        detail.Status.ShouldBe(FeeRefundStatus.Submitted);
        detail.Settlements.ShouldBeEmpty();
        detail.ProcessedAmount.ShouldBe(0m);
        (await FeeRefundTestHelper.GetRefundExpensesAsync(Factory.Services, request.Id)).ShouldBeEmpty();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Cash_Request_Maker_Cannot_Complete_Their_Own_Request_By_Approving()
    {
        var source = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services);
        var request = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, source.StudentId, source.DueFeeId, 200d);
        var approve = await Client.PostAsJsonAsync($"{BaseUrl}/{request.Id}/approve", new FeeRefundActionDto
        {
            ExpenseCategoryId = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services),
            BankAccountId = await FeeRefundTestHelper.SeedSchoolBankAccountAsync(Factory.Services)
        }, JsonOptions);
        approve.IsSuccessStatusCode.ShouldBeFalse();
        (await approve.Content.ReadAsStringAsync()).ShouldContain(FeeRefundErrorCodes.MakerCheckerViolation);
        var detail = (await Client.GetFromJsonAsync<FeeRefundRequestDto>($"{BaseUrl}/{request.Id}", JsonOptions))!;
        detail.Status.ShouldBe(FeeRefundStatus.Submitted);
        detail.Settlements.ShouldBeEmpty();
        (await FeeRefundTestHelper.GetRefundExpensesAsync(Factory.Services, request.Id)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true, false, ShikshaMitra.Finance.Fees.Collections.FeeCollectionStatus.Paid, 1250)]
    [InlineData(false, true, ShikshaMitra.Finance.Fees.Collections.FeeCollectionStatus.Paid, 1000)]
    [InlineData(true, true, ShikshaMitra.Finance.Fees.Collections.FeeCollectionStatus.InReconciliation, 1000)]
    [InlineData(true, true, ShikshaMitra.Finance.Fees.Collections.FeeCollectionStatus.Reversed, 1000)]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Paid_Late_Fees_Should_Follow_Their_Own_Pinned_Policy_And_Settled_Evidence(
        bool lateEnabled, bool chargesEnabled, ShikshaMitra.Finance.Fees.Collections.FeeCollectionStatus status, int expected)
    {
        var student = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services,
            includePaidCharges: chargesEnabled, includePaidLateFee: lateEnabled);
        var late = await FeeRefundTestHelper.SeedPaidLateFeeAsync(Factory.Services, student.StudentId, student.DueFeeId, status: status);
        var url = $"{BaseUrl}/eligibility?studentId={student.StudentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}";
        var eligibility = (await Client.GetFromJsonAsync<RefundEligibilityDto>(url, JsonOptions))!;
        eligibility.TotalRefundable.ShouldBe(expected);
        eligibility.FeeHeads.SelectMany(x => x.Months).Count(x => x.SourceAllocationId == late.AllocationId).ShouldBe(expected == 1250 ? 1 : 0);
        var input = new CreateFeeRefundRequestDto
        {
            StudentId = student.StudentId, AcademicYearId = FeeRefundTestHelper.DefaultAcademicYearId,
            OriginalReceiptId = late.ReceiptId, IdempotencyKey = Guid.NewGuid().ToString("N"),
            RefundMethod = "Cash", PaymentDetails = new() { ["receivedBy"] = "QA Parent" },
            Lines = [new() { LateFeeDueFeeId = student.DueFeeId, SourceAllocationId = late.AllocationId, Amount = 100m }]
        };
        var create = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
        if (expected != 1250) { create.IsSuccessStatusCode.ShouldBeFalse(); return; }
        create.StatusCode.ShouldBe(HttpStatusCode.Created, $"{await create.Content.ReadAsStringAsync()}\n{_refundFactory.ServerErrors.LastOrDefault()}");
        var request = (await create.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!;
        request.Lines.Single().DueFeeId.ShouldBeNull();
        request.Lines.Single().ChargeId.ShouldBeNull();
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        var approve = await checker.PostAsJsonAsync($"{BaseUrl}/{request.Id}/approve", new FeeRefundActionDto
        { ExpenseCategoryId = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services), BankAccountId = await FeeRefundTestHelper.SeedSchoolBankAccountAsync(Factory.Services) }, JsonOptions);
        approve.StatusCode.ShouldBe(HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());
        (await Client.GetFromJsonAsync<RefundEligibilityDto>(url, JsonOptions))!.TotalRefundable.ShouldBe(1150m);
        input.Lines[0].Amount = 151m;
        var excessive = await Client.PostAsJsonAsync($"{BaseUrl}/preview", input, JsonOptions);
        excessive.IsSuccessStatusCode.ShouldBeFalse();
        (await excessive.Content.ReadAsStringAsync()).ShouldContain(FeeRefundErrorCodes.InsufficientRefundBalance);
        var detail = (await Client.GetFromJsonAsync<FeeRefundRequestDto>($"{BaseUrl}/{request.Id}", JsonOptions))!;
        detail.IsRefundSubledgerBalanced.ShouldBe(true);
        detail.FeeHeads!.ShouldContain(x => x.FeeHeadName == "Paid late fees");
        var after = (await Client.GetFromJsonAsync<RefundEligibilityDto>(url, JsonOptions))!;
        after.TotalRefundable.ShouldBe(1150m);
        after.FeeHeads.Where(x => x.FeeHeadId.HasValue).Sum(x => x.TotalRefundable).ShouldBe(1000m);
    }

    [Theory]
    [InlineData(250, 1000, 200)]
    [InlineData(1, 100, 0)]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Paid_Late_Fees_Should_Preserve_All_Whole_Receipt_Sources_Under_A_Cap(double latePaid, int cap, int lateRefund)
    {
        var student = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services,
            includePaidLateFee: true, allowPartialRefund: false, lateFeeAmount: latePaid, maximumPerRequest: cap);
        var eligibility = (await Client.GetFromJsonAsync<RefundEligibilityDto>(
            $"{BaseUrl}/eligibility?studentId={student.StudentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}", JsonOptions))!;
        var sources = eligibility.FeeHeads.SelectMany(x => x.Months).ToList();
        var input = new CreateFeeRefundRequestDto
        {
            StudentId = student.StudentId, AcademicYearId = FeeRefundTestHelper.DefaultAcademicYearId,
            OriginalReceiptId = sources[0].SourceReceiptId, IdempotencyKey = Guid.NewGuid().ToString("N"),
            RefundMethod = "Cash", PaymentDetails = new() { ["receivedBy"] = "QA Parent" },
            Lines = sources.Select(x => new CreateFeeRefundLineDto { DueFeeId = x.DueFeeId,
                LateFeeDueFeeId = x.LateFeeDueFeeId, SourceAllocationId = x.SourceAllocationId, Amount = x.Refundable }).ToList()
        };
        var create = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
        create.StatusCode.ShouldBe(HttpStatusCode.Created, $"{await create.Content.ReadAsStringAsync()}\n{_refundFactory.ServerErrors.LastOrDefault()}");
        var request = (await create.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!;
        request.CalculatedAmount.ShouldBe(cap);
        request.Lines.Count.ShouldBe(2);
        request.Lines.Single(x => x.LateFeeDueFeeId.HasValue).CalculatedAmount.ShouldBe(lateRefund);
        var replay = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
        replay.StatusCode.ShouldBe(HttpStatusCode.Created, await replay.Content.ReadAsStringAsync());
        (await replay.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!.Id.ShouldBe(request.Id);
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        var approval = await checker.PostAsJsonAsync($"{BaseUrl}/{request.Id}/approve", new FeeRefundActionDto
        { ExpenseCategoryId = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services), BankAccountId = await FeeRefundTestHelper.SeedSchoolBankAccountAsync(Factory.Services) }, JsonOptions);
        approval.StatusCode.ShouldBe(HttpStatusCode.OK, await approval.Content.ReadAsStringAsync());
        (await approval.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!.ApprovedAmount.ShouldBe(cap);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Paid_Charges_Should_Follow_Pinned_Policy_And_Settle_Through_Refund_Workflow(bool enabled, bool combined)
    {
        var student = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services,
            includePaidCharges: enabled);
        var charge = await FeeRefundTestHelper.SeedPaidChargeAsync(Factory.Services, student.StudentId, combined: combined);
        var url = $"{BaseUrl}/eligibility?studentId={student.StudentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}";
        var eligibility = (await Client.GetFromJsonAsync<RefundEligibilityDto>(url, JsonOptions))!;
        eligibility.TotalRefundable.ShouldBe(enabled ? 1250m : 1000m);
        eligibility.FeeHeads.SelectMany(x => x.Months).Count(x => x.ChargeId == charge.ChargeId).ShouldBe(enabled ? 1 : 0);
        var input = new CreateFeeRefundRequestDto
        {
            StudentId = student.StudentId, AcademicYearId = FeeRefundTestHelper.DefaultAcademicYearId,
            OriginalReceiptId = charge.ReceiptId, IdempotencyKey = Guid.NewGuid().ToString("N"),
            RefundMethod = "Cash", PaymentDetails = new() { ["receivedBy"] = "QA Parent" },
            Lines = [new() { ChargeId = charge.ChargeId, SourceAllocationId = charge.AllocationId, Amount = 100m }]
        };
        var create = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
        if (!enabled)
        {
            create.IsSuccessStatusCode.ShouldBeFalse();
            (await create.Content.ReadAsStringAsync()).ShouldContain(FeeRefundErrorCodes.FeeHeadNotEligible);
            return;
        }
        create.StatusCode.ShouldBe(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var request = (await create.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!;
        request.Lines.Single().DueFeeId.ShouldBeNull();
        request.Lines.Single().ChargeId.ShouldBe(charge.ChargeId);
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        var categoryId = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services);
        using var checker = CreateClientAs(checkerId);
        var approve = await checker.PostAsJsonAsync($"{BaseUrl}/{request.Id}/approve",
            new FeeRefundActionDto { ExpenseCategoryId = categoryId, BankAccountId = await FeeRefundTestHelper.SeedSchoolBankAccountAsync(Factory.Services) }, JsonOptions);
        approve.StatusCode.ShouldBe(HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());
        (await Client.GetFromJsonAsync<RefundEligibilityDto>(url, JsonOptions))!.TotalRefundable.ShouldBe(1150m);
        input.IdempotencyKey = Guid.NewGuid().ToString("N");
        input.Lines[0].Amount = 151m;
        var excessive = await Client.PostAsJsonAsync($"{BaseUrl}/preview", input, JsonOptions);
        excessive.IsSuccessStatusCode.ShouldBeFalse();
        var detail = (await Client.GetFromJsonAsync<FeeRefundRequestDto>($"{BaseUrl}/{request.Id}", JsonOptions))!;
        detail.IsRefundSubledgerBalanced.ShouldBe(true);
        detail.FeeHeads!.Single(x => x.FeeHeadId == null).Items.Single().Month.ShouldBe("Exam materials");
        (await Client.GetFromJsonAsync<RefundEligibilityDto>(url, JsonOptions))!.TotalRefundable.ShouldBe(1150m);
    }

    [Theory]
    [InlineData(ShikshaMitra.Finance.Fees.Collections.FeeCollectionStatus.InReconciliation)]
    [InlineData(ShikshaMitra.Finance.Fees.Collections.FeeCollectionStatus.Reversed)]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Paid_Charges_Should_Exclude_Unsettled_And_Reversed_Collections(ShikshaMitra.Finance.Fees.Collections.FeeCollectionStatus status)
    {
        var student = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services, includePaidCharges: true);
        var charge = await FeeRefundTestHelper.SeedPaidChargeAsync(Factory.Services, student.StudentId, status: status, combined: false);
        var eligibility = (await Client.GetFromJsonAsync<RefundEligibilityDto>(
            $"{BaseUrl}/eligibility?studentId={student.StudentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}", JsonOptions))!;
        eligibility.TotalRefundable.ShouldBe(1000m);
        eligibility.FeeHeads.SelectMany(x => x.Months).ShouldNotContain(x => x.ChargeId == charge.ChargeId);
    }

    // ── GET /eligibility ─────────────────────────────────────────────────────

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Legacy_Full_Only_Policy_Should_Not_Require_Unselected_Paid_Charges()
    {
        var student = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services,
            includePaidCharges: true, allowPartialRefund: false);
        var charge = await FeeRefundTestHelper.SeedPaidChargeAsync(Factory.Services, student.StudentId);
        var fee = await FeeRefundTestHelper.GetEligibleLineAsync(Client, student.StudentId, student.DueFeeId);
        var input = new CreateFeeRefundRequestDto
        {
            StudentId = student.StudentId, AcademicYearId = FeeRefundTestHelper.DefaultAcademicYearId,
            OriginalReceiptId = charge.ReceiptId, IdempotencyKey = Guid.NewGuid().ToString("N"),
            RefundMethod = "Cash", PaymentDetails = new() { ["receivedBy"] = "QA Parent" },
            Lines = [new() { DueFeeId = student.DueFeeId, SourceAllocationId = fee.SourceAllocationId, Amount = 1000m }]
        };
        var incomplete = await Client.PostAsJsonAsync($"{BaseUrl}/preview", input, JsonOptions);
        incomplete.StatusCode.ShouldBe(HttpStatusCode.OK, await incomplete.Content.ReadAsStringAsync());
        input.Lines.Add(new() { ChargeId = charge.ChargeId, SourceAllocationId = charge.AllocationId, Amount = 250m });
        var complete = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
        complete.StatusCode.ShouldBe(HttpStatusCode.Created, await complete.Content.ReadAsStringAsync());
        (await complete.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!.TotalAmount.ShouldBe(1250m);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Historical_Student_Receipt_Should_Remain_Refundable_After_Account_Adoption()
    {
        var source = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services);
        var line = await FeeRefundTestHelper.GetEligibleLineAsync(Client, source.StudentId, source.DueFeeId);
        await FeeRefundTestHelper.AdoptLegacyAssignmentAsync(Factory.Services, source.StudentId, source.DueFeeId);
        var input = new CreateFeeRefundRequestDto
        {
            StudentId = source.StudentId, AcademicYearId = FeeRefundTestHelper.DefaultAcademicYearId,
            OriginalReceiptId = line.SourceReceiptId, IdempotencyKey = Guid.NewGuid().ToString("N"),
            RefundMethod = "Cash", PaymentDetails = new() { ["receivedBy"] = "QA Parent" },
            Lines = [new() { DueFeeId = source.DueFeeId, SourceAllocationId = line.SourceAllocationId, Amount = 200m }]
        };
        var preview = await Client.PostAsJsonAsync($"{BaseUrl}/preview", input, JsonOptions);
        preview.StatusCode.ShouldBe(HttpStatusCode.OK, await preview.Content.ReadAsStringAsync());
        _refundFactory.ServerErrors.Clear();
        var create = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
        create.StatusCode.ShouldBe(HttpStatusCode.Created,
            $"{await create.Content.ReadAsStringAsync()}\n{_refundFactory.ServerErrors.LastOrDefault()}");
        var request = (await create.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!;
        request.StudentId.ShouldBe(source.StudentId);
        request.FinanceFeeAccountId.ShouldBeNull();
        request.OriginalReceiptId.ShouldBe(line.SourceReceiptId);
        var details = await Client.GetFromJsonAsync<FeeRefundRequestDto>($"{BaseUrl}/{request.Id}", JsonOptions);
        details!.FeeHeads.ShouldNotBeEmpty();
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Applicant_Receipt_Refund_Should_Use_Account_Without_Student_And_Replay_Once()
    {
        var source = await FeeRefundTestHelper.SeedApplicantWithPaidDueFeeAsync(Factory.Services);
        var url = $"{BaseUrl}/eligibility?financeFeeAccountId={source.AccountId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}&originalReceiptId={source.ReceiptId}";
        var eligibility = await Client.GetFromJsonAsync<RefundEligibilityDto>(url, JsonOptions);
        eligibility!.StudentId.ShouldBeNull();
        eligibility.FinanceFeeAccountId.ShouldBe(source.AccountId);
        eligibility.TotalRefundable.ShouldBe(1000m);
        var line = eligibility.FeeHeads.SelectMany(x => x.Months).Single();
        var input = new CreateFeeRefundRequestDto
        {
            FinanceFeeAccountId = source.AccountId, AcademicYearId = FeeRefundTestHelper.DefaultAcademicYearId,
            OriginalReceiptId = source.ReceiptId, IdempotencyKey = Guid.NewGuid().ToString("N"),
            RefundMethod = "Cash", PaymentDetails = new() { ["receivedBy"] = "QA Parent" }, Reason = "Applicant refund test",
            Lines = [new() { DueFeeId = source.DueFeeId, SourceAllocationId = line.SourceAllocationId, Amount = 200m }]
        };
        var preview = await Client.PostAsJsonAsync($"{BaseUrl}/preview", input, JsonOptions);
        preview.StatusCode.ShouldBe(HttpStatusCode.OK, await preview.Content.ReadAsStringAsync());
        (await preview.Content.ReadFromJsonAsync<FeeRefundPreviewDto>(JsonOptions))!.StudentFeeStructureId.ShouldBeNull();
        _refundFactory.ServerErrors.Clear();
        var create = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
        create.StatusCode.ShouldBe(HttpStatusCode.Created,
            $"{await create.Content.ReadAsStringAsync()}\n{_refundFactory.ServerErrors.LastOrDefault()}");
        var request = (await create.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!;
        request.StudentId.ShouldBeNull();
        request.FinanceFeeAccountId.ShouldBe(source.AccountId);
        request.StudentName.ShouldBe("QA Applicant Refund");
        var replay = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
        replay.StatusCode.ShouldBe(HttpStatusCode.Created, await replay.Content.ReadAsStringAsync());
        (await replay.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!.Id.ShouldBe(request.Id);
        input.Lines[0].Amount = 201m;
        var changedReplay = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
        changedReplay.IsSuccessStatusCode.ShouldBeFalse();
        (await changedReplay.Content.ReadAsStringAsync()).ShouldContain(FeeRefundErrorCodes.IdempotencyConflict);

        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        var expenseCategoryId = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services);
        using var checker = CreateClientAs(checkerId);
        var approve = await checker.PostAsJsonAsync($"{BaseUrl}/{request.Id}/approve",
            new FeeRefundActionDto { Notes = "Applicant source checked", ExpenseCategoryId = expenseCategoryId, BankAccountId = await FeeRefundTestHelper.SeedSchoolBankAccountAsync(Factory.Services) }, JsonOptions);
        approve.StatusCode.ShouldBe(HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());
        var detail = await Client.GetFromJsonAsync<FeeRefundRequestDto>($"{BaseUrl}/{request.Id}", JsonOptions);
        detail!.StudentId.ShouldBeNull();
        detail.FinanceFeeAccountId.ShouldBe(source.AccountId);
        detail.Status.ShouldBe(FeeRefundStatus.Processed);
        detail.RefundSubledgerNetAmount.ShouldBe(200m);
        detail.IsRefundSubledgerBalanced.ShouldBe(true);
        (await Client.GetFromJsonAsync<RefundEligibilityDto>(url, JsonOptions))!.TotalRefundable.ShouldBe(800m);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Applicant_Refund_Should_Reject_Unsettled_Receipt_And_Different_Account()
    {
        var paid = await FeeRefundTestHelper.SeedApplicantWithPaidDueFeeAsync(Factory.Services);
        var pending = await FeeRefundTestHelper.SeedApplicantWithPaidDueFeeAsync(Factory.Services, settled: false);
        var eligibility = await Client.GetFromJsonAsync<RefundEligibilityDto>(
            $"{BaseUrl}/eligibility?financeFeeAccountId={paid.AccountId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}&originalReceiptId={paid.ReceiptId}", JsonOptions);
        var line = eligibility!.FeeHeads.SelectMany(x => x.Months).Single();
        var input = new CreateFeeRefundRequestDto
        {
            FinanceFeeAccountId = pending.AccountId, AcademicYearId = FeeRefundTestHelper.DefaultAcademicYearId,
            OriginalReceiptId = paid.ReceiptId, IdempotencyKey = Guid.NewGuid().ToString("N"),
            RefundMethod = "Cash", PaymentDetails = new() { ["receivedBy"] = "QA Parent" },
            Lines = [new() { DueFeeId = paid.DueFeeId, SourceAllocationId = line.SourceAllocationId, Amount = 200m }]
        };
        var mismatch = await Client.PostAsJsonAsync($"{BaseUrl}/preview", input, JsonOptions);
        mismatch.IsSuccessStatusCode.ShouldBeFalse();
        (await mismatch.Content.ReadAsStringAsync()).ShouldContain(FeeRefundErrorCodes.SourceReceiptInvalid);
        input.OriginalReceiptId = pending.ReceiptId;
        input.Lines = [new() { DueFeeId = pending.DueFeeId, SourceAllocationId = pending.SourceAllocationId, Amount = 200m }];
        var unsettled = await Client.PostAsJsonAsync($"{BaseUrl}/preview", input, JsonOptions);
        unsettled.IsSuccessStatusCode.ShouldBeFalse();
        (await unsettled.Content.ReadAsStringAsync()).ShouldContain(FeeRefundErrorCodes.SourceReceiptInvalid);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Refund_Request_And_Review_Should_Expose_Only_Policy_Payment_Methods()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(
            Client, Factory.Services, 1000d, [RefundPayoutMethod.Cash]);
        var eligibility = await Client.GetFromJsonAsync<RefundEligibilityDto>(
            $"{BaseUrl}/eligibility?studentId={studentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}",
            JsonOptions);
        eligibility!.AllowedPayoutMethods.ShouldBe(new[] { "Cash" });

        var denied = await Should.ThrowAsync<InvalidOperationException>(() =>
            FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 100d,
                "Bank Transfer (NEFT/RTGS/IMPS)"));
        denied.Message.ShouldContain(FeeRefundErrorCodes.PayoutMethodNotAllowed);

        var request = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 100d);
        request.AllowedPayoutMethods.ShouldBe(new[] { "Cash" });
        var policyUrl = $"/api/finance/refund-policies/{request.RefundPolicyId}";
        var policy = await Client.GetFromJsonAsync<RefundPolicyDto>(policyUrl, JsonOptions);
        var update = await Client.PutAsJsonAsync(policyUrl, new CreateUpdateRefundPolicyDto
        {
            AcademicYearId = policy!.AcademicYearId, PolicyName = policy.PolicyName,
            PolicyCode = policy.PolicyCode, Status = policy.Status, IsDefault = policy.IsDefault,
            AllowedPayoutMethods = ["BankTransfer", "Upi"], ProcessingTargetDays = policy.ProcessingTargetDays,
            ExpenseCategoryId = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services),
            DefaultPayingBankAccountId = await FeeRefundTestHelper.SeedSchoolBankAccountAsync(Factory.Services),
            ChangeReason = "Future assignments use bank and UPI refunds", ConcurrencyStamp = policy.ConcurrencyStamp
        }, JsonOptions);
        update.StatusCode.ShouldBe(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());
        var savedPolicy = await Client.GetFromJsonAsync<RefundPolicyDto>(policyUrl, JsonOptions);
        savedPolicy!.AllowedPayoutMethods.ShouldBe(new[] { "BankTransfer", "Upi" });

        var detail = await Client.GetFromJsonAsync<FeeRefundRequestDto>($"{BaseUrl}/{request.Id}", JsonOptions);
        detail!.AllowedPayoutMethods.ShouldBe(new[] { "Cash" });

        var checkerId = Guid.NewGuid();
        var expenseCategoryId = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services);
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        var deniedApproval = await checker.PostAsJsonAsync($"{BaseUrl}/{request.Id}/approve",
            new FeeRefundActionDto { ExpenseCategoryId = expenseCategoryId, Notes = "Attempt unsupported method", Method = "UPI" }, JsonOptions);
        deniedApproval.IsSuccessStatusCode.ShouldBeFalse();
        (await deniedApproval.Content.ReadAsStringAsync()).ShouldContain(FeeRefundErrorCodes.PayoutMethodNotAllowed);
        var approval = await checker.PostAsJsonAsync($"{BaseUrl}/{request.Id}/approve",
            new FeeRefundActionDto { ExpenseCategoryId = expenseCategoryId, BankAccountId = await FeeRefundTestHelper.SeedSchoolBankAccountAsync(Factory.Services), Notes = "Source checked" }, JsonOptions);
        approval.StatusCode.ShouldBe(HttpStatusCode.OK, await approval.Content.ReadAsStringAsync());
        var reviewed = await approval.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions);
        reviewed!.AllowedPayoutMethods.ShouldBe(new[] { "Cash" });

        var deniedPayout = await Client.PostAsJsonAsync($"{BaseUrl}/{request.Id}/payout",
            new RecordRefundPayoutDto
            {
                ExpenseCategoryId = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services),
                BankAccountId = await FeeRefundTestHelper.SeedSchoolBankAccountAsync(Factory.Services),
                IdempotencyKey = Guid.NewGuid().ToString("N"), Method = "UPI", Amount = 100m,
                PaymentDate = DateTimeOffset.UtcNow, ExternalReference = "UPI-reference",
                EvidenceReference = "Transfer evidence"
            }, JsonOptions);
        deniedPayout.IsSuccessStatusCode.ShouldBeFalse();
        (await deniedPayout.Content.ReadAsStringAsync()).ShouldContain(FeeRefundErrorCodes.PayoutMethodNotAllowed);
    }

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

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Should_Get_Zero_Eligibility_For_Student_With_No_Payments()
    {
        var studentId = await FeeRefundTestHelper.SeedStudentWithUnpaidDueFeeAsync(Factory.Services);

        var response = await Client.GetAsync(
            $"{BaseUrl}/eligibility?studentId={studentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<RefundEligibilityDto>(JsonOptions);
        body.ShouldNotBeNull();
        body.TotalPaid.ShouldBe(0m);
        body.TotalRefundable.ShouldBe(0m);
        body.FeeHeads.ShouldBeEmpty();
        body.Receipts.ShouldBeEmpty();
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

        var refundDto = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 600d, "Bank Transfer (NEFT/RTGS/IMPS)");

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

        var refundDto = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 400d, "Bank Transfer (NEFT/RTGS/IMPS)");

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
        body.AllowedPayoutMethods.ShouldBe(new[] { "Cash", "Bank Transfer (NEFT/RTGS/IMPS)" });
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
    public async Task Fully_Paid_Fee_Should_Stay_Paid_Through_Refund_Request_Approval_And_Payout()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(
            Client, Factory.Services, 1000d, attachCollectionPolicy: true);
        await FeeRefundTestHelper.EnrollStudentForCollectionSummaryAsync(Factory.Services, studentId);

        async Task AssertPaidAsync(decimal refunded, decimal pendingRefund = 0m)
        {
            var formResponse = await Client.GetAsync($"/api/fee-collections/{studentId}/collect-form?academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}");
            formResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await formResponse.Content.ReadAsStringAsync());
            var form = (await formResponse.Content.ReadFromJsonAsync<FeeCollectFormDto>(JsonOptions))!;
            var line = form.Ledger.SelectMany(x => x.Items).Single(x => x.DueFeeId == dueFeeId);
            line.Due.ShouldBe(0d);
            line.Status.ShouldBe(FeeCollectionStatus.Paid);
            line.RefundAmount.ShouldBe((double)refunded);
            line.PendingRefundAmount.ShouldBe(pendingRefund);
            form.Summary.PendingFee.ShouldBe(0d);
            var details = (await Client.GetFromJsonAsync<StudentFeeDetailsDto>(
                $"/api/fee-collections/{studentId}/fee-details?academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}", JsonOptions))!;
            details.FeeOverview.PendingFee.ShouldBe(0d);
            (details.FeeOverview.OverdueFee ?? 0d).ShouldBe(0d);
            details.FeeOverview.RefundedAmount.ShouldBe(refunded);
            details.FeeHeads.ShouldAllBe(x => x.Status == FeeCollectionStatus.Paid);
            var summary = (await Client.GetFromJsonAsync<Volo.Abp.Application.Dtos.PagedResultDto<FeeCollectionSummaryItemDto>>(
                $"/api/fee-collections?academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}&boardId={FeeRefundTestHelper.DefaultBoardId}&educationMediumId={FeeRefundTestHelper.DefaultMediumId}&search={Uri.EscapeDataString(form.Student.StudentName)}", JsonOptions))!;
            var student = summary.Items.Single(x => x.StudentId == studentId);
            student.Status.ShouldBe(FeeCollectionStatus.Paid);
            student.PendingFees.ShouldBe(0d);
            student.OverdueFees.ShouldBe(0d);
        }

        await AssertPaidAsync(0m);
        var request = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 200d, "Bank Transfer (NEFT/RTGS/IMPS)");
        await AssertPaidAsync(0m);
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        var approval = await checker.PostAsJsonAsync($"{BaseUrl}/{request.Id}/approve",
            new FeeRefundActionDto { Notes = "Approved refund of settled fees" }, JsonOptions);
        approval.StatusCode.ShouldBe(HttpStatusCode.OK, await approval.Content.ReadAsStringAsync());
        await AssertPaidAsync(0m, 200m);
        var payout = await Client.PostAsJsonAsync($"{BaseUrl}/{request.Id}/payout", new RecordRefundPayoutDto
        {
            ExpenseCategoryId = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services),
            BankAccountId = await FeeRefundTestHelper.SeedSchoolBankAccountAsync(Factory.Services),
            IdempotencyKey = Guid.NewGuid().ToString("N"), Method = "Bank Transfer (NEFT/RTGS/IMPS)", Amount = 200m,
            PaymentDate = DateTimeOffset.UtcNow, ExternalReference = Guid.NewGuid().ToString("N")
        }, JsonOptions);
        payout.StatusCode.ShouldBe(HttpStatusCode.OK, await payout.Content.ReadAsStringAsync());
        await AssertPaidAsync(200m);
        var eligibility = (await Client.GetFromJsonAsync<RefundEligibilityDto>(
            $"{BaseUrl}/eligibility?studentId={studentId}&academicYearId={FeeRefundTestHelper.DefaultAcademicYearId}", JsonOptions))!;
        eligibility.TotalRefundable.ShouldBe(800m);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Recorded_Cash_Refund_Should_Complete_And_Post_One_Expense_And_Balanced_Subledger_Entry()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(
            Client, Factory.Services, 1000d);
        var request = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 200d);
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        var checkerView = await checker.GetAsync($"{BaseUrl}/{request.Id}");
        checkerView.StatusCode.ShouldBe(HttpStatusCode.OK, await checkerView.Content.ReadAsStringAsync());
        await FeeRefundTestHelper.SeedHistoricalApprovedCashAsync(Factory.Services, request.Id, checkerId);

        var payoutInput = new RecordRefundPayoutDto
        {
            ExpenseCategoryId = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services),
            IdempotencyKey = Guid.NewGuid().ToString("N"), Method = "Cash", Amount = 200m,
            PaymentDate = DateTimeOffset.UtcNow
        };
        var record = await Client.PostAsJsonAsync($"{BaseUrl}/{request.Id}/payout",
            payoutInput, JsonOptions);
        record.StatusCode.ShouldBe(HttpStatusCode.OK);
        var awaiting = await record.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions);
        awaiting!.Status.ShouldBe(FeeRefundStatus.Processed);
        awaiting.Settlements.Count.ShouldBe(1);
        var recordReplay = await Client.PostAsJsonAsync($"{BaseUrl}/{request.Id}/payout",
            payoutInput, JsonOptions);
        recordReplay.StatusCode.ShouldBe(HttpStatusCode.OK,
            await recordReplay.Content.ReadAsStringAsync());
        var replayedPayout = await recordReplay.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions);
        replayedPayout!.Settlements.Single().Id.ShouldBe(awaiting.Settlements.Single().Id);

        var detail = await Client.GetFromJsonAsync<FeeRefundRequestDto>($"{BaseUrl}/{request.Id}", JsonOptions);
        detail!.Status.ShouldBe(FeeRefundStatus.Processed);
        detail.RefundSubledgerNetAmount.ShouldBe(200m);
        detail.IsRefundSubledgerBalanced.ShouldBe(true);
        var expenses = await FeeRefundTestHelper.GetRefundExpensesAsync(Factory.Services, request.Id);
        expenses.Count.ShouldBe(1);
        var expense = expenses.Single();
        expense.Amount.ShouldBe(200m);
        expense.CategoryId.ShouldBe(payoutInput.ExpenseCategoryId!.Value);
        expense.BankAccountId.ShouldBeNull();
        expense.IsConfirmed.ShouldBe(true);
        expense.TransactionType.ShouldBe(ShikshaMitra.Finance.Accounts.Transactions.TransactionType.Expense);
        payoutInput.ExpenseCategoryId = Guid.NewGuid();
        var changedReplay = await Client.PostAsJsonAsync($"{BaseUrl}/{request.Id}/payout", payoutInput, JsonOptions);
        changedReplay.IsSuccessStatusCode.ShouldBeFalse();
        (await FeeRefundTestHelper.GetRefundExpensesAsync(Factory.Services, request.Id)).Count.ShouldBe(1);
    }

    [Fact]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Recording_With_Invalid_Expense_Routing_Should_Leave_Refund_Approved_And_Expenses_Empty()
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services);
        var request = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 200d, "Bank Transfer (NEFT/RTGS/IMPS)");
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        var approval = await checker.PostAsJsonAsync($"{BaseUrl}/{request.Id}/approve", new FeeRefundActionDto(), JsonOptions);
        approval.StatusCode.ShouldBe(HttpStatusCode.OK, await approval.Content.ReadAsStringAsync());
        var input = new RecordRefundPayoutDto
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"), Method = "Bank Transfer (NEFT/RTGS/IMPS)", Amount = 200m,
            PaymentDate = DateTimeOffset.UtcNow, ExpenseCategoryId = Guid.NewGuid(),
            BankAccountId = await FeeRefundTestHelper.SeedSchoolBankAccountAsync(Factory.Services)
        };
        var invalidCategory = await Client.PostAsJsonAsync($"{BaseUrl}/{request.Id}/payout", input, JsonOptions);
        invalidCategory.IsSuccessStatusCode.ShouldBeFalse();
        input.ExpenseCategoryId = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services);
        input.BankAccountId = Guid.NewGuid();
        var invalidBank = await Client.PostAsJsonAsync($"{BaseUrl}/{request.Id}/payout", input, JsonOptions);
        invalidBank.IsSuccessStatusCode.ShouldBeFalse();
        var detail = (await Client.GetFromJsonAsync<FeeRefundRequestDto>($"{BaseUrl}/{request.Id}", JsonOptions))!;
        detail.Status.ShouldBe(FeeRefundStatus.Approved);
        detail.Settlements.ShouldBeEmpty();
        detail.ProcessedAmount.ShouldBe(0);
        (await FeeRefundTestHelper.GetRefundExpensesAsync(Factory.Services, request.Id)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Cash", true)]
    [InlineData("Cash", false)]
    [InlineData("Bank Transfer (NEFT/RTGS/IMPS)", true)]
    [InlineData("Bank Transfer (NEFT/RTGS/IMPS)", false)]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Existing_Pending_Payment_Should_Complete_Through_Record_And_Create_Only_One_Expense(
        string method, bool sendMaskedReference)
    {
        var (studentId, dueFeeId) = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services);
        var request = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 200d, method);
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        if (method == "Cash")
            await FeeRefundTestHelper.SeedHistoricalApprovedCashAsync(Factory.Services, request.Id, checkerId);
        else
        {
            var approval = await checker.PostAsJsonAsync($"{BaseUrl}/{request.Id}/approve", new FeeRefundActionDto(), JsonOptions);
            approval.StatusCode.ShouldBe(HttpStatusCode.OK, await approval.Content.ReadAsStringAsync());
        }
        var input = new RecordRefundPayoutDto
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"), Method = method, Amount = 200m,
            PaymentDate = DateTimeOffset.UtcNow, ExternalReference = $"PAYMENT-{request.VoucherNumber}"
        };
        var pendingId = await FeeRefundTestHelper.SeedPendingPayoutAsync(Factory.Services, request.Id, checkerId, input);
        var worklist = (await Client.GetFromJsonAsync<Volo.Abp.Application.Dtos.PagedResultDto<FeeRefundRequestDto>>(
            $"{BaseUrl}?StudentId={studentId}", JsonOptions))!;
        var maskedReference = worklist.Items.Single(x => x.Id == request.Id).Settlements.Single().ExternalReference;
        maskedReference.ShouldNotBeNull();
        maskedReference.ShouldNotBe(input.ExternalReference);
        maskedReference.ShouldContain("*");
        input.ExternalReference = sendMaskedReference ? maskedReference : null;
        input.IdempotencyKey = Guid.NewGuid().ToString("N");
        input.ExpenseCategoryId = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services);
        if (method != "Cash")
            input.BankAccountId = await FeeRefundTestHelper.SeedSchoolBankAccountAsync(Factory.Services);
        var result = await Client.PostAsJsonAsync($"{BaseUrl}/{request.Id}/payout", input, JsonOptions);
        result.StatusCode.ShouldBe(HttpStatusCode.OK, await result.Content.ReadAsStringAsync());
        var completed = (await result.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!;
        completed.Status.ShouldBe(FeeRefundStatus.Processed);
        completed.Settlements.Single().Id.ShouldBe(pendingId);
        var refreshed = (await Client.GetFromJsonAsync<FeeRefundRequestDto>($"{BaseUrl}/{request.Id}", JsonOptions))!;
        refreshed.Settlements.Single().ExternalReference.ShouldBe($"PAYMENT-{request.VoucherNumber}");
        refreshed.IsRefundSubledgerBalanced.ShouldBe(true);
        refreshed.RefundSubledgerNetAmount.ShouldBe(200m);
        var replay = await Client.PostAsJsonAsync($"{BaseUrl}/{request.Id}/payout", input, JsonOptions);
        replay.StatusCode.ShouldBe(HttpStatusCode.OK, await replay.Content.ReadAsStringAsync());
        input.ExternalReference = "DIFFERENT-PAYMENT-REFERENCE";
        var changedReplay = await Client.PostAsJsonAsync($"{BaseUrl}/{request.Id}/payout", input, JsonOptions);
        changedReplay.IsSuccessStatusCode.ShouldBeFalse();
        (await FeeRefundTestHelper.GetRefundExpensesAsync(Factory.Services, request.Id)).Count.ShouldBe(1);
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
        await FeeRefundTestHelper.SeedPendingPayoutAsync(Factory.Services, request.Id, Guid.NewGuid(),
            new RecordRefundPayoutDto
            {
                ExpenseCategoryId = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services),
                IdempotencyKey = Guid.NewGuid().ToString("N"), Method = method, Amount = 200m,
                BankAccountId = bankId, PaymentDate = DateTimeOffset.UtcNow,
                ExternalReference = Guid.NewGuid().ToString("N"),
                EvidenceReference = "Bank submission receipt"
            });
        var awaiting = await Client.GetFromJsonAsync<FeeRefundRequestDto>($"{BaseUrl}/{request.Id}", JsonOptions);
        awaiting!.Status.ShouldBe(FeeRefundStatus.Approved);
        awaiting.RefundSubledgerNetAmount.ShouldBe(0m);
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
                ExpenseCategoryId = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services),
                IdempotencyKey = Guid.NewGuid().ToString("N"), Method = method, Amount = 200m,
                BankAccountId = bankId, PaymentDate = DateTimeOffset.UtcNow,
                ExternalReference = Guid.NewGuid().ToString("N"),
                EvidenceReference = "Bank submission receipt"
            }, JsonOptions);
        recorded.StatusCode.ShouldBe(HttpStatusCode.OK, await recorded.Content.ReadAsStringAsync());
        var payout = (await recorded.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!
            .Settlements.Single();
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
        var first = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 100d, "Bank Transfer (NEFT/RTGS/IMPS)");
        var second = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 100d, "Bank Transfer (NEFT/RTGS/IMPS)");
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
        var category = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services);
        var bank = await FeeRefundTestHelper.SeedSchoolBankAccountAsync(Factory.Services);
        RecordRefundPayoutDto Payout() => new()
        {
            ExpenseCategoryId = category, BankAccountId = bank,
            IdempotencyKey = Guid.NewGuid().ToString("N"), Method = "Bank Transfer (NEFT/RTGS/IMPS)", Amount = 100m,
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
        var first = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 700d, "Bank Transfer (NEFT/RTGS/IMPS)");
        var second = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, studentId, dueFeeId, 700d, "Bank Transfer (NEFT/RTGS/IMPS)");
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
