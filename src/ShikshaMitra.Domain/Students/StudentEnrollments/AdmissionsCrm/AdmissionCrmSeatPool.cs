using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmSeatPool : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid? PlaybookId { get; private set; }
	public string Code { get; private set; } = string.Empty;
	public string Name { get; private set; } = string.Empty;
	public string? RoundCode { get; private set; }
	public string? Campus { get; private set; }
	public string? DesiredClass { get; private set; }
	public string? Board { get; private set; }
	public string? Medium { get; private set; }
	public string? ApplicantCategory { get; private set; }
	public int OperationalTarget { get; private set; }
	public int ApprovedCapacity { get; private set; }
	public int ReservedSeats { get; private set; }
	public int AcceptedSeats { get; private set; }
	public int ActivatedSeats { get; private set; }
	public int WaitlistedApplicants { get; private set; }
	public int ReleasedSeats { get; private set; }
	public int CancelledSeats { get; private set; }
	public AdmissionCrmSeatPoolStatus Status { get; private set; }

	public int OccupiedSeats => ReservedSeats + AcceptedSeats + ActivatedSeats;
	public int RemainingCapacity => Math.Max(0, ApprovedCapacity - OccupiedSeats);
	public bool IsOverCapacity => OccupiedSeats > ApprovedCapacity;

	protected AdmissionCrmSeatPool()
	{
	}

	public AdmissionCrmSeatPool(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid? playbookId,
		string code,
		string name,
		string? roundCode,
		string? campus,
		string? desiredClass,
		string? board,
		string? medium,
		string? applicantCategory,
		int operationalTarget,
		int approvedCapacity) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Code = Check.NotNullOrWhiteSpace(code, nameof(code), AdmissionCrmConsts.MaxSeatPoolCodeLength);
		Status = AdmissionCrmSeatPoolStatus.Active;
		UpdateDefinition(
			playbookId, name, roundCode, campus, desiredClass, board, medium, applicantCategory,
			operationalTarget, approvedCapacity, true);
	}

	public void UpdateDefinition(
		Guid? playbookId,
		string name,
		string? roundCode,
		string? campus,
		string? desiredClass,
		string? board,
		string? medium,
		string? applicantCategory,
		int operationalTarget,
		int approvedCapacity,
		bool isActive)
	{
		if (operationalTarget < 0 || approvedCapacity < 0 || operationalTarget > approvedCapacity)
		{
			throw new BusinessException("AdmissionCrm:InvalidSeatPoolCapacity");
		}
		if (approvedCapacity < OccupiedSeats)
		{
			throw new BusinessException("AdmissionCrm:SeatPoolCapacityBelowCommittedSeats")
				.WithData("OccupiedSeats", OccupiedSeats)
				.WithData("ApprovedCapacity", approvedCapacity);
		}

		PlaybookId = playbookId;
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), AdmissionCrmConsts.MaxNameLength);
		RoundCode = Normalize(roundCode, AdmissionCrmConsts.MaxPlaybookCodeLength);
		Campus = Normalize(campus, AdmissionCrmConsts.MaxNameLength);
		DesiredClass = Normalize(desiredClass, AdmissionCrmConsts.MaxNameLength);
		Board = Normalize(board, AdmissionCrmConsts.MaxNameLength);
		Medium = Normalize(medium, AdmissionCrmConsts.MaxNameLength);
		ApplicantCategory = Normalize(applicantCategory, AdmissionCrmConsts.MaxNameLength);
		OperationalTarget = operationalTarget;
		ApprovedCapacity = approvedCapacity;
		Status = isActive ? AdmissionCrmSeatPoolStatus.Active : AdmissionCrmSeatPoolStatus.Closed;
	}

	public bool ReserveSeat(bool capacityOverride, string? overrideReason)
	{
		if (Status != AdmissionCrmSeatPoolStatus.Active)
		{
			throw new BusinessException("AdmissionCrm:SeatPoolClosed");
		}

		var requiresOverride = OccupiedSeats >= ApprovedCapacity;
		if (requiresOverride && !capacityOverride)
		{
			throw new BusinessException("AdmissionCrm:SeatPoolCapacityExhausted")
				.WithData("SeatPoolId", Id)
				.WithData("ApprovedCapacity", ApprovedCapacity);
		}
		if (requiresOverride && string.IsNullOrWhiteSpace(overrideReason))
		{
			throw new BusinessException("AdmissionCrm:SeatCapacityOverrideReasonRequired");
		}

		ReservedSeats++;
		return requiresOverride;
	}

	public void AcceptReservedSeat()
	{
		if (ReservedSeats <= 0)
		{
			throw new BusinessException("AdmissionCrm:SeatReservationCounterMismatch");
		}
		ReservedSeats--;
		AcceptedSeats++;
	}

	public void ReleaseSeat(bool wasAccepted, bool cancelled)
	{
		if (wasAccepted)
		{
			if (AcceptedSeats <= 0)
			{
				throw new BusinessException("AdmissionCrm:AcceptedSeatCounterMismatch");
			}
			AcceptedSeats--;
		}
		else
		{
			if (ReservedSeats <= 0)
			{
				throw new BusinessException("AdmissionCrm:SeatReservationCounterMismatch");
			}
			ReservedSeats--;
		}

		if (cancelled)
		{
			CancelledSeats++;
		}
		else
		{
			ReleasedSeats++;
		}
	}

	public void MarkActivated()
	{
		if (AcceptedSeats <= 0)
		{
			throw new BusinessException("AdmissionCrm:AcceptedSeatCounterMismatch");
		}
		AcceptedSeats--;
		ActivatedSeats++;
	}

	public void CancelCommittedSeat()
	{
		if (ActivatedSeats > 0)
		{
			ActivatedSeats--;
		}
		else if (AcceptedSeats > 0)
		{
			AcceptedSeats--;
		}
		else
		{
			throw new BusinessException("AdmissionCrm:CommittedSeatCounterMismatch");
		}
		CancelledSeats++;
	}

	public void AddWaitlistedApplicant() => WaitlistedApplicants++;

	public void RemoveWaitlistedApplicant()
	{
		if (WaitlistedApplicants <= 0)
		{
			throw new BusinessException("AdmissionCrm:WaitlistCounterMismatch");
		}
		WaitlistedApplicants--;
	}

	private static string? Normalize(string? value, int maxLength) =>
		string.IsNullOrWhiteSpace(value) ? null : Check.Length(value.Trim(), nameof(value), maxLength);
}
