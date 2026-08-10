using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmEvent : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	private readonly List<AdmissionCrmEventRegistration> _registrations = [];

	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Title { get; private set; } = string.Empty;
	public AdmissionCrmEventType EventType { get; private set; }
	public DateTime StartsAt { get; private set; }
	public DateTime EndsAt { get; private set; }
	public string Location { get; private set; } = string.Empty;
	public int Capacity { get; private set; }
	public Guid? OwnerUserId { get; private set; }
	public string? Instructions { get; private set; }
	public bool AllowFamilySelfScheduling { get; private set; }
	public AdmissionCrmEventStatus Status { get; private set; }
	public DateTime? PublishedAt { get; private set; }
	public DateTime? CancelledAt { get; private set; }
	public string? CancellationReason { get; private set; }
	public IReadOnlyCollection<AdmissionCrmEventRegistration> Registrations => _registrations;

	public int ReservedCount => _registrations.Count(x =>
		x.Status is AdmissionCrmEventRegistrationStatus.Reserved or
			AdmissionCrmEventRegistrationStatus.CheckedIn);

	protected AdmissionCrmEvent()
	{
	}

	public AdmissionCrmEvent(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		string title,
		AdmissionCrmEventType eventType,
		DateTime startsAt,
		DateTime endsAt,
		string location,
		int capacity,
		Guid? ownerUserId,
		string? instructions,
		bool allowFamilySelfScheduling = false)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		UpdateDetails(title, eventType, startsAt, endsAt, location, capacity, ownerUserId, instructions,
			allowFamilySelfScheduling);
		Status = AdmissionCrmEventStatus.Draft;
	}

	public void UpdateDetails(
		string title,
		AdmissionCrmEventType eventType,
		DateTime startsAt,
		DateTime endsAt,
		string location,
		int capacity,
		Guid? ownerUserId,
		string? instructions,
		bool allowFamilySelfScheduling = false)
	{
		EnsureEditable();
		if (endsAt <= startsAt)
		{
			throw new BusinessException("AdmissionCrm:EventEndMustFollowStart");
		}
		if (capacity is < 1 or > 5000 || capacity < ReservedCount)
		{
			throw new BusinessException("AdmissionCrm:InvalidEventCapacity")
				.WithData("ReservedCount", ReservedCount);
		}

		Title = Check.NotNullOrWhiteSpace(title, nameof(title), AdmissionCrmConsts.MaxDescriptionLength);
		EventType = eventType;
		StartsAt = startsAt;
		EndsAt = endsAt;
		Location = Check.NotNullOrWhiteSpace(location, nameof(location), AdmissionCrmConsts.MaxDescriptionLength);
		Capacity = capacity;
		OwnerUserId = ownerUserId;
		Instructions = NormalizeOptional(instructions, nameof(instructions), AdmissionCrmConsts.MaxNotesLength);
		AllowFamilySelfScheduling = allowFamilySelfScheduling;
	}

	public void Publish(DateTime publishedAt)
	{
		if (Status != AdmissionCrmEventStatus.Draft)
		{
			throw new BusinessException("AdmissionCrm:EventNotDraft");
		}
		if (StartsAt <= publishedAt)
		{
			throw new BusinessException("AdmissionCrm:EventMustStartInFuture");
		}
		Status = AdmissionCrmEventStatus.Published;
		PublishedAt = publishedAt;
	}

	public AdmissionCrmEventRegistration Reserve(Guid id, Guid leadId, DateTime reservedAt, string? notes)
	{
		if (Status != AdmissionCrmEventStatus.Published || StartsAt <= reservedAt)
		{
			throw new BusinessException("AdmissionCrm:EventNotOpenForRegistration");
		}
		if (_registrations.Any(x => x.LeadId == leadId && x.Status != AdmissionCrmEventRegistrationStatus.Cancelled))
		{
			throw new BusinessException("AdmissionCrm:EventApplicantAlreadyRegistered");
		}
		if (ReservedCount >= Capacity)
		{
			throw new BusinessException("AdmissionCrm:EventCapacityReached");
		}

		var registration = new AdmissionCrmEventRegistration(
			id,
			TenantId,
			Id,
			leadId,
			reservedAt,
			notes);
		_registrations.Add(registration);
		return registration;
	}

	public void CheckIn(Guid registrationId, DateTime checkedInAt)
	{
		if (Status != AdmissionCrmEventStatus.Published)
		{
			throw new BusinessException("AdmissionCrm:EventNotOpenForAttendance");
		}
		if (checkedInAt < StartsAt.AddHours(-4))
		{
			throw new BusinessException("AdmissionCrm:EventCheckInTooEarly");
		}
		GetRegistration(registrationId).CheckIn(checkedInAt);
	}

	public void MarkNoShow(Guid registrationId, DateTime recordedAt)
	{
		if (Status != AdmissionCrmEventStatus.Published)
		{
			throw new BusinessException("AdmissionCrm:EventNotOpenForAttendance");
		}
		if (recordedAt < StartsAt)
		{
			throw new BusinessException("AdmissionCrm:EventNoShowBeforeStart");
		}
		GetRegistration(registrationId).MarkNoShow(recordedAt);
	}

	public void CancelRegistration(Guid registrationId, DateTime cancelledAt, string? reason)
	{
		GetRegistration(registrationId).Cancel(cancelledAt, reason);
	}

	public void Cancel(DateTime cancelledAt, string reason)
	{
		if (Status is AdmissionCrmEventStatus.Cancelled or AdmissionCrmEventStatus.Completed)
		{
			throw new BusinessException("AdmissionCrm:EventCannotBeCancelled");
		}
		Status = AdmissionCrmEventStatus.Cancelled;
		CancelledAt = cancelledAt;
		CancellationReason = Check.NotNullOrWhiteSpace(
			reason,
			nameof(reason),
			AdmissionCrmConsts.MaxNotesLength);
		foreach (var registration in _registrations.Where(x =>
			         x.Status == AdmissionCrmEventRegistrationStatus.Reserved))
		{
			registration.Cancel(cancelledAt, "Event cancelled by the school.");
		}
	}

	public void Complete(DateTime completedAt)
	{
		if (Status != AdmissionCrmEventStatus.Published || completedAt < EndsAt)
		{
			throw new BusinessException("AdmissionCrm:EventCannotBeCompleted");
		}
		Status = AdmissionCrmEventStatus.Completed;
	}

	private AdmissionCrmEventRegistration GetRegistration(Guid registrationId) =>
		_registrations.SingleOrDefault(x => x.Id == registrationId) ??
		throw new BusinessException("AdmissionCrm:EventRegistrationNotFound");

	private void EnsureEditable()
	{
		if (Status is AdmissionCrmEventStatus.Cancelled or AdmissionCrmEventStatus.Completed)
		{
			throw new BusinessException("AdmissionCrm:EventNotEditable");
		}
	}

	private static string? NormalizeOptional(string? value, string name, int maxLength) =>
		string.IsNullOrWhiteSpace(value) ? null : Check.Length(value.Trim(), name, maxLength);
}

public class AdmissionCrmEventRegistration : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid EventId { get; private set; }
	public Guid LeadId { get; private set; }
	public AdmissionCrmEventRegistrationStatus Status { get; private set; }
	public DateTime ReservedAt { get; private set; }
	public DateTime? CheckedInAt { get; private set; }
	public DateTime? CancelledAt { get; private set; }
	public DateTime? NoShowRecordedAt { get; private set; }
	public string? Notes { get; private set; }
	public string? CancellationReason { get; private set; }

	protected AdmissionCrmEventRegistration()
	{
	}

	internal AdmissionCrmEventRegistration(
		Guid id,
		Guid? tenantId,
		Guid eventId,
		Guid leadId,
		DateTime reservedAt,
		string? notes)
		: base(id)
	{
		TenantId = tenantId;
		EventId = eventId;
		LeadId = leadId;
		ReservedAt = reservedAt;
		Status = AdmissionCrmEventRegistrationStatus.Reserved;
		Notes = string.IsNullOrWhiteSpace(notes)
			? null
			: Check.Length(notes.Trim(), nameof(notes), AdmissionCrmConsts.MaxNotesLength);
	}

	internal void CheckIn(DateTime checkedInAt)
	{
		EnsureReserved();
		Status = AdmissionCrmEventRegistrationStatus.CheckedIn;
		CheckedInAt = checkedInAt;
	}

	internal void MarkNoShow(DateTime recordedAt)
	{
		EnsureReserved();
		Status = AdmissionCrmEventRegistrationStatus.NoShow;
		NoShowRecordedAt = recordedAt;
	}

	internal void Cancel(DateTime cancelledAt, string? reason)
	{
		EnsureReserved();
		Status = AdmissionCrmEventRegistrationStatus.Cancelled;
		CancelledAt = cancelledAt;
		CancellationReason = string.IsNullOrWhiteSpace(reason)
			? null
			: Check.Length(reason.Trim(), nameof(reason), AdmissionCrmConsts.MaxNotesLength);
	}

	private void EnsureReserved()
	{
		if (Status != AdmissionCrmEventRegistrationStatus.Reserved)
		{
			throw new BusinessException("AdmissionCrm:EventRegistrationNotReserved");
		}
	}
}
