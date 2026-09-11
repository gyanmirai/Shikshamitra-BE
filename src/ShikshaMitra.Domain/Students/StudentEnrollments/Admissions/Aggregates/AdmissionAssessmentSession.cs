using System;
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

	public void Reserve(DateTime now)
	{
		if (IsCancelled || StartsAt <= now)
			throw new BusinessException(AdmissionAssessmentErrorCodes.InvalidState, "Choose a future assessment session.");
		if (BookedCount >= Capacity)
			throw new BusinessException(AdmissionAssessmentErrorCodes.CapacityExceeded, "This assessment session is full.");
		BookedCount++;
	}
	public void Release() { if (BookedCount > 0) BookedCount--; }
	public void Cancel()
	{
		if (BookedCount > 0)
			throw new BusinessException(AdmissionAssessmentErrorCodes.InvalidState, "Cancel applicant bookings before cancelling this session.");
		IsCancelled = true;
	}
}
