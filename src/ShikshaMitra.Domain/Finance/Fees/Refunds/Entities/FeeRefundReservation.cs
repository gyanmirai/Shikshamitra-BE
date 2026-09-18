using System;
using Volo.Abp.Domain.Entities;
namespace ShikshaMitra.Finance.Fees.FeeRefund;
public class FeeRefundReservation : Entity<Guid>
{
    public Guid FeeRefundRequestId { get; private set; }
    public Guid FeeRefundLineId { get; private set; }
    public Guid DueFeeId { get; private set; }
    public Guid? SourceAllocationId { get; private set; }
    public decimal Amount { get; private set; }
    public FeeRefundReservationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    private FeeRefundReservation() { }
    internal FeeRefundReservation(Guid id, Guid requestId, Guid lineId, Guid dueFeeId, Guid? allocationId, decimal amount) : base(id)
    { FeeRefundRequestId = requestId; FeeRefundLineId = lineId; DueFeeId = dueFeeId; SourceAllocationId = allocationId; Amount = amount; Status = FeeRefundReservationStatus.Active; CreatedAt = DateTimeOffset.UtcNow; }
    internal void Consume() { if (Status != FeeRefundReservationStatus.Active) throw new InvalidOperationException("Reservation is not active."); Status = FeeRefundReservationStatus.Consumed; }
    internal void Release() { if (Status != FeeRefundReservationStatus.Active) return; Status = FeeRefundReservationStatus.Released; ReleasedAt = DateTimeOffset.UtcNow; }
}
