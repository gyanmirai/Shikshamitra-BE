using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentSession : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AdmissionOfferingId { get; private set; }
	public Guid WorkflowStageDefinitionId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public DateTime StartsAt { get; private set; }
	public DateTime EndsAt { get; private set; }
	public string Location { get; private set; } = string.Empty;
	public int Capacity { get; private set; }
	public int BookedCount { get; private set; }
	public bool IsCancelled { get; private set; }
	public string SessionType { get; private set; } = "exam";
	public Guid? RoomId { get; private set; }
	public Guid? RoomReservationId { get; private set; }
	public int? RoomReservationStatus { get; set; }
	public bool RoomReady => !RoomReservationId.HasValue || RoomReservationStatus is 3 or 4 or 5 or 6;
	public Guid? InterviewerId { get; private set; }
	public int? SlotDurationMinutes { get; private set; }
	public int? BreakMinutes { get; private set; }
	public ICollection<AdmissionAssessmentSessionScope> Scopes { get; private set; } = [];

	protected AdmissionAssessmentSession() { }
	public AdmissionAssessmentSession(Guid id, Guid? tenantId, Guid offeringId, Guid definitionId,
		string name, DateTime startsAt, DateTime endsAt, string location, int capacity) : base(id)
	{
		TenantId = tenantId;
		AdmissionOfferingId = offeringId;
		WorkflowStageDefinitionId = definitionId;
		Update(name, startsAt, endsAt, location, capacity);
	}

	public void Update(string name, DateTime startsAt, DateTime endsAt, string location, int capacity)
	{
		if (IsCancelled || endsAt <= startsAt || capacity < 1 || capacity > 10000 || capacity < BookedCount)
			throw new BusinessException(AdmissionAssessmentErrorCodes.InvalidState, "Check the session dates and capacity.");
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), 128).Trim();
		Location = Check.NotNullOrWhiteSpace(location, nameof(location), 256).Trim();
		StartsAt = startsAt.ToUniversalTime();
		EndsAt = endsAt.ToUniversalTime();
		Capacity = capacity;
	}

	public void Reserve(DateTime now, DateTime? appointmentStartsAt = null)
	{
		if (SessionType == "individualInterview" && (!appointmentStartsAt.HasValue || GetInterviewSlots().All(slot => slot.StartsAt != appointmentStartsAt.Value)))
			throw new BusinessException(AdmissionAssessmentErrorCodes.InvalidState, "Choose a derived interview appointment.");
		if (IsCancelled || (SessionType == "individualInterview" ? appointmentStartsAt ?? StartsAt : StartsAt) <= now)
			throw new BusinessException(AdmissionAssessmentErrorCodes.InvalidState, "Choose a future assessment session.");
		if (BookedCount >= Capacity)
			throw new BusinessException(AdmissionAssessmentErrorCodes.CapacityExceeded, "This assessment session is full.");
		BookedCount++;
	}
	public void ConfigurePlanning(string type, Guid roomId, Guid reservationId, Guid? interviewerId, int? duration, int? gap,
		IEnumerable<AdmissionAssessmentSessionScope> scopes)
	{
		if (type is not ("exam" or "individualInterview" or "groupInterview") || roomId == Guid.Empty || reservationId == Guid.Empty ||
			type != "exam" && (!interviewerId.HasValue || interviewerId == Guid.Empty) ||
			type == "individualInterview" && (duration is null or < 5 or > 180 || gap is null or < 0 or > 60))
			throw new BusinessException(AdmissionAssessmentErrorCodes.InvalidState, "Choose a valid room, interviewer and interview duration.");
		var links = scopes.ToList();
		if (links.Count is < 1 or > 20 || links.Any(link => link.SessionId != Id) ||
			links.Any(link => link.TenantId != TenantId || link.AdmissionOfferingId == Guid.Empty || link.WorkflowStageDefinitionId == Guid.Empty) ||
			links.Select(link => (link.AdmissionOfferingId, link.WorkflowStageDefinitionId)).Distinct().Count() != links.Count)
			throw new BusinessException(AdmissionAssessmentErrorCodes.InvalidState, "Choose distinct assessment scopes.");
		SessionType = type; RoomId = roomId; RoomReservationId = reservationId; InterviewerId = interviewerId;
		SlotDurationMinutes = type == "individualInterview" ? duration : null;
		BreakMinutes = type == "individualInterview" ? gap : null; Scopes = links;
	}
	public bool Supports(Guid offeringId, Guid stageId) =>
		Scopes.Count == 0 ? AdmissionOfferingId == offeringId && WorkflowStageDefinitionId == stageId :
		Scopes.Any(scope => scope.AdmissionOfferingId == offeringId && scope.WorkflowStageDefinitionId == stageId);
	public IReadOnlyList<(DateTime StartsAt, DateTime EndsAt)> GetInterviewSlots()
	{
		if (SessionType != "individualInterview" || !SlotDurationMinutes.HasValue) return [];
		var slots = new List<(DateTime, DateTime)>();
		for (var start = StartsAt; start.AddMinutes(SlotDurationMinutes.Value) <= EndsAt && slots.Count < Capacity;
			start = start.AddMinutes(SlotDurationMinutes.Value + BreakMinutes.GetValueOrDefault()))
			slots.Add((start, start.AddMinutes(SlotDurationMinutes.Value)));
		return slots;
	}
	public void Release() { if (BookedCount > 0) BookedCount--; }
	public void Cancel()
	{
		if (BookedCount > 0)
			throw new BusinessException(AdmissionAssessmentErrorCodes.InvalidState, "Cancel applicant bookings before cancelling this session.");
		IsCancelled = true;
	}
}
