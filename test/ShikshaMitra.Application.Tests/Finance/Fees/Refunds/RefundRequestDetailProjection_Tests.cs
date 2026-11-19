using System;
using System.Linq;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class RefundRequestDetailProjection_Tests
{
    [Fact]
    public void Approved_request_uses_only_saved_lines_and_does_not_inherit_previous_refunds()
    {
        var dto = Request(FeeRefundStatus.Approved);
        dto.FeeHeads = [new RefundFeeHeadDto { FeeHeadName = "Other tuition fees", TotalRefunded = 50000 }];
        dto.TotalRefundedAmount = 50000;
        var dueId = dto.Lines.Single().DueFeeId!.Value;

        RefundRequestDetailProjection.Populate(dto,
            new System.Collections.Generic.Dictionary<Guid, RefundDueDisplayFacts> { [dueId] = new(15000m, 2) });

        dto.RefundRequestId.ShouldBe(dto.Id);
        dto.TotalFeeHeads.ShouldBe(1);
        dto.TotalRefundedAmount.ShouldBe(0);
        dto.TotalPaidAmount.ShouldBe(3750);
        var head = dto.FeeHeads.Single();
        head.FeeHeadName.ShouldBe("General School Fees");
        head.TotalRefunded.ShouldBe(0);
        var installment = head.Items.Single();
        installment.Month.ShouldBe("December 2026");
        installment.RefundAmount.ShouldBe(3750);
        installment.Total.ShouldBe(15000);
        installment.Paid.ShouldBe(3750);
    }

    [Theory]
    [InlineData(FeeRefundStatus.Submitted, 3750)]
    [InlineData(FeeRefundStatus.Approved, 3750)]
    [InlineData(FeeRefundStatus.Processed, 3500)]
    public void Displays_the_stored_amount_for_the_current_request_stage(FeeRefundStatus status, int amount)
    {
        var dto = Request(status);
        dto.Lines[0].Amount = 6000m;
        dto.Lines[0].SettledPaidAmount = 6000m;
        dto.Lines[0].ProcessedAmount = 3500m;
        dto.ProcessedAmount = 3500m;
        RefundRequestDetailProjection.Populate(dto);
        dto.FeeHeads.Single().Items.Single().RefundAmount.ShouldBe((double)amount);
    }

    [Fact]
    public void Combines_selected_allocations_of_one_installment_without_duplicating_its_due_total()
    {
        var dto = Request(FeeRefundStatus.Approved);
        var first = dto.Lines[0];
        first.Amount = first.ApprovedAmount = first.SettledPaidAmount = 1250m;
        dto.Lines.Add(new FeeRefundLineDto { Id = Guid.NewGuid(), DueFeeId = first.DueFeeId,
            FeeHeadId = first.FeeHeadId, FeeHeadName = first.FeeHeadName, InstallmentLabel = first.InstallmentLabel,
            Amount = 2500m, ApprovedAmount = 2500m, SettledPaidAmount = 2500m });
        RefundRequestDetailProjection.Populate(dto,
            new System.Collections.Generic.Dictionary<Guid, RefundDueDisplayFacts> { [first.DueFeeId!.Value] = new(15000m, 2) });
        var item = dto.FeeHeads.Single().Items.Single();
        item.RefundAmount.ShouldBe(3750);
        item.Paid.ShouldBe(3750);
        item.Total.ShouldBe(15000);
    }

    [Fact]
    public void Returned_payout_reports_only_this_requests_net_refunded_amount()
    {
        var dto = Request(FeeRefundStatus.Failed);
        dto.ProcessedAmount = dto.ReversedAmount = 3750m;
        dto.Lines[0].ProcessedAmount = dto.Lines[0].ReversedAmount = 3750m;
        RefundRequestDetailProjection.Populate(dto);
        dto.TotalRefundedAmount.ShouldBe(0);
        dto.FeeHeads.Single().TotalRefunded.ShouldBe(0);
        dto.FeeHeads.Single().Items.Single().RefundAmount.ShouldBe(3750);
    }

    [Fact]
    public void Keeps_distinct_saved_fee_heads_when_older_lines_have_no_head_id()
    {
        var dto = Request(FeeRefundStatus.Approved);
        dto.Lines[0].FeeHeadId = null;
        dto.Lines.Add(new FeeRefundLineDto { Id = Guid.NewGuid(), DueFeeId = Guid.NewGuid(),
            FeeHeadName = "General Tuition Fee", InstallmentLabel = "July 2026", ApprovedAmount = 500m });
        RefundRequestDetailProjection.Populate(dto);
        dto.FeeHeads.Select(head => head.FeeHeadName).ShouldBe(new[] { "General School Fees", "General Tuition Fee" });
    }

    [Fact]
    public void Keeps_charge_and_late_fee_sources_in_distinct_groups()
    {
        var dto = Request(FeeRefundStatus.Approved);
        dto.Lines.Add(new FeeRefundLineDto { Id = Guid.NewGuid(), ChargeId = Guid.NewGuid(),
            InstallmentLabel = "Library charge", SettledPaidAmount = 500m, Amount = 500m, ApprovedAmount = 500m });
        dto.Lines.Add(new FeeRefundLineDto { Id = Guid.NewGuid(), LateFeeDueFeeId = Guid.NewGuid(),
            InstallmentLabel = "July late fee", SettledPaidAmount = 250m, Amount = 250m, ApprovedAmount = 250m });
        RefundRequestDetailProjection.Populate(dto);
        dto.TotalFeeHeads.ShouldBe(3);
        dto.FeeHeads.Select(head => head.FeeHeadName).ShouldBe(new[] { "General School Fees", "Paid charges", "Paid late fees" });
        dto.FeeHeads.SelectMany(head => head.Items).Sum(item => item.RefundAmount).ShouldBe(4500);
    }

    private static FeeRefundRequestDto Request(FeeRefundStatus status) => new()
    {
        Id = Guid.NewGuid(), Status = status, ApprovedAmount = 3750m,
        Lines = [new FeeRefundLineDto { Id = Guid.NewGuid(), DueFeeId = Guid.NewGuid(), FeeHeadId = Guid.NewGuid(),
            FeeHeadName = "General School Fees", InstallmentLabel = "December 2026", SettledPaidAmount = 3750m,
            Amount = 3750m, CalculatedAmount = 3750m, ApprovedAmount = 3750m }]
    };
}
