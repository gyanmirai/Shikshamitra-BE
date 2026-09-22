using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
namespace ShikshaMitra.Finance.Fees.FeeRefund;
public class FeeRefundSettlement : Entity<Guid>
{
    public Guid FeeRefundRequestId { get; private set; }
    public int AttemptNumber { get; private set; }
    public Guid InitiatedByUserId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public FeeRefundSettlementStatus Status { get; private set; }
    public string Method { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public DateTimeOffset SettlementDate { get; private set; }
    public string? ExternalReference { get; private set; }
    public string? PaymentDetailsJson { get; private set; }
    public string? FailureReason { get; private set; }
    public string? ReversalReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    private FeeRefundSettlement() { }
    internal FeeRefundSettlement(Guid id, Guid requestId, int attempt, Guid userId, string key, string method, decimal amount, DateTimeOffset date, string? reference, string? details) : base(id)
    { FeeRefundRequestId = requestId; AttemptNumber = attempt; InitiatedByUserId = userId; IdempotencyKey = Check.NotNullOrWhiteSpace(key, nameof(key), 100); Method = Check.NotNullOrWhiteSpace(method, nameof(method), 100); Amount = amount; SettlementDate = date; ExternalReference = reference?.Trim(); PaymentDetailsJson = details; Status = FeeRefundSettlementStatus.Processing; CreatedAt = DateTimeOffset.UtcNow; }
    internal void MarkProcessed() { Status = FeeRefundSettlementStatus.Processed; CompletedAt = DateTimeOffset.UtcNow; }
    internal void MarkFailed(string reason) { Status = FeeRefundSettlementStatus.Failed; FailureReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 1000); CompletedAt = DateTimeOffset.UtcNow; }
    internal void MarkReversed(string reason) { if (Status != FeeRefundSettlementStatus.Processed) throw new InvalidOperationException("Only processed settlements can be reversed."); Status = FeeRefundSettlementStatus.Reversed; ReversalReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 1000); }
}
