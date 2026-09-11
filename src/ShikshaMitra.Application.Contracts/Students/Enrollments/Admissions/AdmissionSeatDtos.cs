using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionSeatEventDto
{
	public Guid Id { get; set; }
	public string Action { get; set; } = string.Empty;
	public string? Reason { get; set; }
	public Guid? ActorId { get; set; }
	public DateTime OccurredAt { get; set; }
	public string? MetadataJson { get; set; }
}

public class AdmissionSeatStageDto
{
	public Guid ApplicationId { get; set; }
	public Guid AdmissionOfferingId { get; set; }
	public Guid StageId { get; set; }
	public bool SeatStageEnabled { get; set; }
	public AdmissionSeatStagePolicy? Policy { get; set; }
	public bool IsActive { get; set; }
	public string Status { get; set; } = "ready";
	public int Capacity { get; set; }
	public int ReservedCount { get; set; }
	public int ConfirmedCount { get; set; }
	public int RemainingCapacity { get; set; }
	public bool IsOverCapacity { get; set; }
	public Guid? ReservationId { get; set; }
	public DateTime? ReservedAt { get; set; }
	public DateTime? ExpiresAt { get; set; }
	public DateTime? ConfirmedAt { get; set; }
	public int RenewalCount { get; set; }
	public int? WaitlistPosition { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public string? PoolConcurrencyStamp { get; set; }
	public string? ReservationConcurrencyStamp { get; set; }
	public List<AdmissionSeatEventDto> Events { get; set; } = [];
	public List<string> Blockers { get; set; } = [];
	public bool CanReserve { get; set; }
	public bool CanConfirm { get; set; }
	public bool CanRenew { get; set; }
	public bool CanRelease { get; set; }
	public bool CanJoinWaitlist { get; set; }
	public bool CanManageCapacity { get; set; }
	public bool CanComplete { get; set; }
}

public class ReserveAdmissionSeatDto
{
	public DateTime? ExpiresAt { get; set; }
	public bool JoinWaitlistWhenFull { get; set; } = true;
	public bool OverrideCapacity { get; set; }
	public string? Reason { get; set; }
	public string IdempotencyKey { get; set; } = string.Empty;
	public string? ConcurrencyStamp { get; set; }
	public string? ReservationConcurrencyStamp { get; set; }
}

public class ConfirmAdmissionSeatDto
{
	public string? Reason { get; set; }
	public string IdempotencyKey { get; set; } = string.Empty;
	public string? ConcurrencyStamp { get; set; }
	public string? ReservationConcurrencyStamp { get; set; }
}

public class RenewAdmissionSeatDto
{
	public DateTime ExpiresAt { get; set; }
	public string? Reason { get; set; }
	public string IdempotencyKey { get; set; } = string.Empty;
	public string? ConcurrencyStamp { get; set; }
	public string? ReservationConcurrencyStamp { get; set; }
}

public class AdmissionSeatReasonDto
{
	public string? Reason { get; set; }
	public string IdempotencyKey { get; set; } = string.Empty;
	public string? ConcurrencyStamp { get; set; }
	public string? ReservationConcurrencyStamp { get; set; }
}

public class UpdateAdmissionSeatCapacityDto
{
	public int Capacity { get; set; }
	public bool AllowBelowOccupied { get; set; }
	public string Reason { get; set; } = string.Empty;
	public string IdempotencyKey { get; set; } = string.Empty;
	public string? ConcurrencyStamp { get; set; }
	public string? PoolConcurrencyStamp { get; set; }
}
