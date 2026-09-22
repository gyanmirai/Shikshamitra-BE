using System;
using System.Linq;
using ShikshaMitra.Finance.Fees.FeeRefund;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Fees;

public class FeeRefundRequestLifecycle_Tests
{
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

    private static FeeRefundRequest CreateSubmittedRequest()
    {
        var request = new FeeRefundRequest(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "REF-TEST-0001", "Cash", new CashRefundDetails(DateOnly.FromDateTime(DateTime.UtcNow), "Parent"),
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
