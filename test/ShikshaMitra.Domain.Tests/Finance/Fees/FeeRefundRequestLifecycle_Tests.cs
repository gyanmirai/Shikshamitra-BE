using System;
using System.Linq;
using ShikshaMitra.Finance.Fees.FeeRefund;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Fees;

public class FeeRefundRequestLifecycle_Tests
{
    [Fact]
    public void Capped_refund_retains_zero_rounded_source_evidence_without_reserving_or_paying_it()
    {
        var request = new FeeRefundRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "REF-CAPPED", "Cash", null, "Capped whole receipt");
        var receiptId = Guid.NewGuid();
        request.PinPolicy(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), receiptId, "RCPT-CAPPED", "{}", "{}", new string('a', 64));
        var principal = new FeeRefundLine(Guid.NewGuid(), request.Id, Guid.NewGuid(), Guid.NewGuid(), receiptId,
            Guid.NewGuid(), "Tuition", "June", 1000m, 1000m, 100m);
        var late = new FeeRefundLine(Guid.NewGuid(), request.Id, null, Guid.NewGuid(), receiptId,
            null, "Paid late fees", "June", 1m, 1m, 0m, lateFeeDueFeeId: principal.DueFeeId);
        request.AddLine(principal);
        request.AddLine(late);
        request.Submit(Guid.NewGuid());
        request.Approve(Guid.NewGuid(), [new FeeRefundReservation(Guid.NewGuid(), request.Id, principal.Id,
            principal.DueFeeId, principal.SourceAllocationId, 100m)]);
        Assert.Single(request.Reservations);
        Assert.Equal(100m, request.ApprovedAmount);
        Assert.Equal(0m, late.ApprovedAmount);
        var settlement = request.StartProcessing(Guid.NewGuid(), Guid.NewGuid(), "capped", "Cash", 100m,
            DateTimeOffset.UtcNow, "CASH-CAPPED", null);
        request.CompleteSettlement(settlement.Id, Guid.NewGuid());
        Assert.Equal(100m, request.ProcessedAmount);
        Assert.Equal(0m, late.ProcessedAmount);
        request.Reverse(settlement.Id, Guid.NewGuid(), "Returned capped payout");
        Assert.Equal(100m, request.ReversedAmount);
        Assert.Equal(0m, late.ReversedAmount);
        Assert.Equal(2, request.Lines.Count);
    }

    [Fact]
    public void Legacy_request_is_initialized_with_lines_before_entering_submitted_state()
    {
        var requestId = Guid.NewGuid();
        var request = new FeeRefundRequest(
            requestId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "REF-SEED-TEST",
            [
                new FeeRefundLine(Guid.NewGuid(), requestId, Guid.NewGuid(), 500d)
            ],
            "Cash", null, "Seeded legacy refund");

        Assert.Equal(FeeRefundStatus.Submitted, request.Status);
        Assert.Single(request.Lines);
        Assert.Equal(500m, request.TotalAmount);
        Assert.Equal(500m, request.CalculatedAmount);
    }

    [Fact]
    public void Approval_reserves_the_refund_without_marking_it_processed()
    {
        var request = CreateSubmittedRequest();
        var line = request.Lines.Single();
        var reservation = new FeeRefundReservation(
            Guid.NewGuid(), request.Id, line.Id, line.DueFeeId, line.SourceAllocationId, line.RequestedAmount);

        request.Approve(Guid.NewGuid(), [reservation], "Checked against the original receipt");

        Assert.Equal(FeeRefundStatus.Approved, request.Status);
        Assert.Equal(500m, request.ApprovedAmount);
        Assert.Equal(0m, request.ProcessedAmount);
        Assert.Equal(FeeRefundReservationStatus.Active, request.Reservations.Single().Status);
        Assert.Equal(0m, line.ProcessedAmount);
    }

    [Fact]
    public void Settlement_is_idempotent_and_reversal_preserves_a_compensating_record()
    {
        var request = CreateApprovedRequest();
        var processor = Guid.NewGuid();
        var settlementId = Guid.NewGuid();
        const string idempotencyKey = "refund-test-key";

        var settlement = request.StartProcessing(
            settlementId, processor, idempotencyKey, "Cash", 500m,
            DateTimeOffset.UtcNow, "CASH-001", "{\"receivedBy\":\"Parent\"}");
        request.CompleteSettlement(settlement.Id, processor);

        var replay = request.StartProcessing(
            Guid.NewGuid(), processor, idempotencyKey, "Cash", 500m,
            DateTimeOffset.UtcNow, "CASH-001", null);

        Assert.Same(settlement, replay);
        Assert.Single(request.Settlements);
        Assert.Equal(FeeRefundStatus.Processed, request.Status);
        Assert.Equal(500m, request.ProcessedAmount);
        Assert.Equal(FeeRefundReservationStatus.Consumed, request.Reservations.Single().Status);

        request.Reverse(settlement.Id, Guid.NewGuid(), "Bank confirmed that the payout was returned");

        Assert.Equal(FeeRefundStatus.Reversed, request.Status);
        Assert.Equal(500m, request.ReversedAmount);
        Assert.Equal(FeeRefundSettlementStatus.Reversed, settlement.Status);
        Assert.Equal(500m, request.Lines.Single().ReversedAmount);
    }

    [Fact]
    public void Failed_settlement_can_retry_with_a_new_idempotency_key()
    {
        var request = CreateApprovedRequest();
        var processor = Guid.NewGuid();
        var failed = request.StartProcessing(
            Guid.NewGuid(), processor, "first-attempt", "UPI", 500m,
            DateTimeOffset.UtcNow, null, null);
        request.FailSettlement(failed.Id, "Payment provider timeout");

        var retry = request.StartProcessing(
            Guid.NewGuid(), processor, "second-attempt", "UPI", 500m,
            DateTimeOffset.UtcNow, "UPI-002", null);

        Assert.Equal(FeeRefundStatus.Processing, request.Status);
        Assert.Equal(2, retry.AttemptNumber);
        Assert.Equal(FeeRefundSettlementStatus.Failed, failed.Status);
        Assert.Equal(FeeRefundSettlementStatus.Processing, retry.Status);
    }

    [Fact]
    public void Cancelling_an_approved_request_releases_its_reservation()
    {
        var request = CreateApprovedRequest();

        request.Cancel(Guid.NewGuid(), "The applicant withdrew the request");

        Assert.Equal(FeeRefundStatus.Cancelled, request.Status);
        Assert.Equal(FeeRefundReservationStatus.Released, request.Reservations.Single().Status);
        Assert.Throws<BusinessException>(() => request.StartProcessing(
            Guid.NewGuid(), Guid.NewGuid(), "after-cancel", "Cash", 500m,
            DateTimeOffset.UtcNow, null, null));
    }

    [Fact]
    public void New_workflow_keeps_an_uncertain_payout_approved_until_verified()
    {
        var request = CreateSubmittedRequest();
        request.UseSimpleWorkflow(Guid.NewGuid(), "create-once");
        var line = request.Lines.Single();
        request.Approve(Guid.NewGuid(),
        [new FeeRefundReservation(Guid.NewGuid(), request.Id, line.Id, line.DueFeeId,
            line.SourceAllocationId, line.RequestedAmount)]);

        var payout = request.RecordPayout(Guid.NewGuid(), Guid.NewGuid(), "payout-once",
            "Cash", 500m, DateTimeOffset.UtcNow, "HANDOVER-1", null, null, "signed-copy-1");

        Assert.Equal(FeeRefundStatus.Approved, request.Status);
        Assert.Equal(0m, request.ProcessedAmount);
        Assert.Throws<BusinessException>(() => request.RecordPayout(Guid.NewGuid(), Guid.NewGuid(),
            "another-attempt", "Cash", 500m, DateTimeOffset.UtcNow, "HANDOVER-2", null,
            null, "signed-copy-2"));

        request.ConfirmPayout(payout.Id, Guid.NewGuid(), "verify-once", "signed-copy-1");
        Assert.Equal(FeeRefundStatus.Processed, request.Status);
        Assert.Equal(500m, request.ProcessedAmount);
        Assert.Equal(FeeRefundReservationStatus.Consumed, request.Reservations.Single().Status);
    }

    [Fact]
    public void New_workflow_failure_is_terminal_and_releases_reservation()
    {
        var request = CreateSubmittedRequest();
        request.UseSimpleWorkflow(Guid.NewGuid(), "create-once");
        var line = request.Lines.Single();
        request.Approve(Guid.NewGuid(),
        [new FeeRefundReservation(Guid.NewGuid(), request.Id, line.Id, line.DueFeeId,
            line.SourceAllocationId, line.RequestedAmount)]);
        var payout = request.RecordPayout(Guid.NewGuid(), Guid.NewGuid(), "payout-once",
            "Cash", 500m, DateTimeOffset.UtcNow, "HANDOVER-1", null, null, "signed-copy-1");

        request.FailPayout(payout.Id, Guid.NewGuid(), "verify-failure",
            "Recipient did not accept the cash", "signed-refusal-1");

        Assert.Equal(FeeRefundStatus.Failed, request.Status);
        Assert.Equal(0m, request.ProcessedAmount);
        Assert.Equal(FeeRefundReservationStatus.Released, request.Reservations.Single().Status);
        Assert.Throws<BusinessException>(() => request.RecordPayout(Guid.NewGuid(), Guid.NewGuid(),
            "another-attempt", "Cash", 500m, DateTimeOffset.UtcNow, "HANDOVER-2", null,
            null, "signed-copy-2"));
    }

    [Theory]
    [InlineData("Cash")]
    [InlineData("BankTransfer")]
    [InlineData("UPI")]
    [InlineData("Cheque")]
    [InlineData("DemandDraft")]
    public void New_workflow_uses_one_payout_lifecycle_for_every_method(string method)
    {
        var request = CreateSubmittedRequest(method);
        request.UseSimpleWorkflow(Guid.NewGuid(), Guid.NewGuid().ToString("N"));
        var line = request.Lines.Single();
        request.Approve(Guid.NewGuid(),
        [new FeeRefundReservation(Guid.NewGuid(), request.Id, line.Id, line.DueFeeId,
            line.SourceAllocationId, line.RequestedAmount)]);
        var payout = request.RecordPayout(Guid.NewGuid(), Guid.NewGuid(), "attempt-1", method,
            500m, DateTimeOffset.UtcNow, "reference-1", null,
            method == "Cash" ? null : Guid.NewGuid(), "payment evidence");
        Assert.Equal(FeeRefundStatus.Approved, request.Status);
        request.ConfirmPayout(payout.Id, Guid.NewGuid(), "verification-1", "verification evidence");
        Assert.Equal(FeeRefundStatus.Processed, request.Status);
        if (method == "Cash") return;
        request.RecordReturnedPayout(payout.Id, Guid.NewGuid(), "Bank returned the payout");
        Assert.Equal(FeeRefundStatus.Failed, request.Status);
        Assert.Equal(request.ProcessedAmount, request.ReversedAmount);
    }

    private static FeeRefundRequest CreateApprovedRequest()
    {
        var request = CreateSubmittedRequest();
        var line = request.Lines.Single();
        request.Approve(Guid.NewGuid(),
        [
            new FeeRefundReservation(
                Guid.NewGuid(), request.Id, line.Id, line.DueFeeId, line.SourceAllocationId, line.RequestedAmount)
        ]);
        return request;
    }

    private static FeeRefundRequest CreateSubmittedRequest(string method = "Cash")
    {
        var request = new FeeRefundRequest(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "REF-TEST-0001", method,
            method == "Cash" ? new CashRefundDetails(DateOnly.FromDateTime(DateTime.UtcNow), "Parent") : null,
            "Student transfer");
        var receiptId = Guid.NewGuid();
        request.PinPolicy(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), receiptId, "RCPT-0001",
            "{\"policy\":\"snapshot\"}", "{\"calculation\":\"snapshot\"}", new string('a', 64));
        request.AddLine(new FeeRefundLine(
            Guid.NewGuid(), request.Id, Guid.NewGuid(), Guid.NewGuid(), receiptId,
            Guid.NewGuid(), "Tuition fee", "April 2026", 1000m, 500m, 500m));
        request.Submit(Guid.NewGuid());
        return request;
    }
}
