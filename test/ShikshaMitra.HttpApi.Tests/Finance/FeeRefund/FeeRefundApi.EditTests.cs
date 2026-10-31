using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.FeeRefundTests;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.FeeRefunds;

public partial class FeeRefundApi_Tests
{
    [Fact]
    public async Task Pending_edit_should_preserve_identity_and_reject_a_stale_retry()
    {
        var student = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services);
        var saved = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, student.StudentId, student.DueFeeId, 200d);
        saved.Status.ShouldBe(FeeRefundStatus.Submitted);
        saved.AvailableActions.ShouldContain("edit");
        var input = EditInput(saved, 150m);
        input.StudentId = Guid.NewGuid();
        input.AcademicYearId = Guid.NewGuid();
        var response = await Client.PutAsJsonAsync($"{BaseUrl}/{saved.Id}", input, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var updated = (await response.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!;
        updated.Id.ShouldBe(saved.Id);
        updated.StudentId.ShouldBe(saved.StudentId);
        updated.AcademicYearId.ShouldBe(saved.AcademicYearId);
        updated.OriginalReceiptId.ShouldBe(saved.OriginalReceiptId);
        updated.RefundPolicyVersionId.ShouldBe(saved.RefundPolicyVersionId);
        updated.Status.ShouldBe(FeeRefundStatus.Submitted);
        updated.CalculatedAmount.ShouldBe(150m);
        updated.ApprovedAmount.ShouldBe(0m);
        updated.ProcessedAmount.ShouldBe(0m);
        updated.ConcurrencyStamp.ShouldNotBe(saved.ConcurrencyStamp);
        var stale = await Client.PutAsJsonAsync($"{BaseUrl}/{saved.Id}", input, JsonOptions);
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Pending_editor_cannot_approve_their_changes_and_approved_refunds_cannot_be_edited()
    {
        var student = await FeeRefundTestHelper.SeedStudentWithPaidDueFeeAsync(Client, Factory.Services);
        var saved = await FeeRefundTestHelper.CreateRefundRequestAsync(Client, student.StudentId, student.DueFeeId, 200d);
        var editorId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, editorId);
        using var editor = CreateClientAs(editorId);
        var edited = await editor.PutAsJsonAsync($"{BaseUrl}/{saved.Id}", EditInput(saved, 150m), JsonOptions);
        edited.StatusCode.ShouldBe(HttpStatusCode.OK, await edited.Content.ReadAsStringAsync());
        var changed = (await edited.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!;
        changed.AvailableActions.ShouldNotContain("approve");
        var category = await FeeRefundTestHelper.SeedRefundExpenseCategoryAsync(Factory.Services);
        var selfApproval = await editor.PostAsJsonAsync($"{BaseUrl}/{saved.Id}/approve",
            new FeeRefundActionDto { ExpenseCategoryId = category }, JsonOptions);
        selfApproval.IsSuccessStatusCode.ShouldBeFalse();
        var checkerId = Guid.NewGuid();
        await FeeRefundTestHelper.SeedCheckerScopeAsync(Factory.Services, checkerId);
        using var checker = CreateClientAs(checkerId);
        var approval = await checker.PostAsJsonAsync($"{BaseUrl}/{saved.Id}/approve",
            new FeeRefundActionDto { ExpenseCategoryId = category }, JsonOptions);
        approval.StatusCode.ShouldBe(HttpStatusCode.OK, await approval.Content.ReadAsStringAsync());
        var approved = (await approval.Content.ReadFromJsonAsync<FeeRefundRequestDto>(JsonOptions))!;
        approved.AvailableActions.ShouldNotContain("edit");
        var forbidden = await editor.PutAsJsonAsync($"{BaseUrl}/{saved.Id}", EditInput(approved, 100m), JsonOptions);
        forbidden.IsSuccessStatusCode.ShouldBeFalse();
        (await Client.GetFromJsonAsync<FeeRefundRequestDto>($"{BaseUrl}/{saved.Id}", JsonOptions))!
            .ApprovedAmount.ShouldBe(150m);
    }

    private static UpdateFeeRefundRequestDto EditInput(FeeRefundRequestDto saved, decimal amount) => new()
    {
        ConcurrencyStamp = saved.ConcurrencyStamp,
        StudentId = saved.StudentId,
        AcademicYearId = saved.AcademicYearId,
        OriginalReceiptId = saved.OriginalReceiptId!.Value,
        RefundMethod = saved.RefundMethod,
        PaymentDetails = saved.PaymentDetails,
        Reason = "Corrected pending refund",
        Lines = saved.Lines.Select(line => new CreateFeeRefundLineDto
        {
            DueFeeId = line.DueFeeId, ChargeId = line.ChargeId, LateFeeDueFeeId = line.LateFeeDueFeeId,
            SourceAllocationId = line.SourceAllocationId!.Value, Amount = amount,
        }).ToList(),
    };
}
