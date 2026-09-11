using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionSeatPool : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AdmissionOfferingId { get; private set; }
	public int Capacity { get; private set; }
	public int ReservedCount { get; private set; }
	public int ConfirmedCount { get; private set; }

	public int OccupiedCount => ReservedCount + ConfirmedCount;
	public int RemainingCapacity => Math.Max(0, Capacity - OccupiedCount);
	public bool IsOverCapacity => OccupiedCount > Capacity;

	protected AdmissionSeatPool() { }

	public AdmissionSeatPool(Guid id, Guid? tenantId, Guid admissionOfferingId, int capacity) : base(id)
	{
		TenantId = tenantId;
		AdmissionOfferingId = Check.NotDefaultOrNull<Guid>(admissionOfferingId, nameof(admissionOfferingId));
		SetCapacity(capacity, allowBelowOccupied: false);
	}

	public void Reserve(bool allowCapacityOverride)
	{
		if (!allowCapacityOverride && OccupiedCount >= Capacity)
			throw new BusinessException(AdmissionSeatStageErrorCodes.CapacityFull);
		ReservedCount++;
	}

	public void Confirm()
	{
		if (ReservedCount <= 0) throw Invalid("No reserved seat is available to confirm.");
		ReservedCount--;
		ConfirmedCount++;
	}

	public void Release(AdmissionSeatReservationStatus status)
	{
		if (status == AdmissionSeatReservationStatus.Reserved && ReservedCount > 0) ReservedCount--;
		else if (status == AdmissionSeatReservationStatus.Confirmed && ConfirmedCount > 0) ConfirmedCount--;
	}

	public void ExpireReserved(int count)
	{
		if (count < 0 || count > ReservedCount) throw Invalid("The expired reservation count is invalid.");
		ReservedCount -= count;
	}

	public void SetCapacity(int capacity, bool allowBelowOccupied)
	{
		if (capacity is < 1 or > 100000)
			throw new BusinessException(AdmissionSeatStageErrorCodes.CapacityInvalid);
		if (!allowBelowOccupied && capacity < OccupiedCount)
			throw new BusinessException(AdmissionSeatStageErrorCodes.CapacityInvalid,
				"Capacity cannot be lower than the number of reserved and confirmed seats.");
		Capacity = capacity;
	}

	private static BusinessException Invalid(string message) =>
		new(AdmissionSeatStageErrorCodes.InvalidState, message);
}
