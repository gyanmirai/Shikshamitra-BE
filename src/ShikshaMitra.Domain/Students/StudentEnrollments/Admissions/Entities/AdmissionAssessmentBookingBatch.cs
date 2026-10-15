using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentBookingBatch : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid SessionId { get; private set; }
	public Guid ActorId { get; private set; }
	public string Fingerprint { get; private set; } = string.Empty;
	public int BookedCount { get; private set; }
	public DateTime CreationTime { get; private set; }
	protected AdmissionAssessmentBookingBatch() { }
	public AdmissionAssessmentBookingBatch(Guid id, Guid? tenantId, Guid sessionId, Guid actorId, string fingerprint, int bookedCount) : base(id)
	{ TenantId = tenantId; SessionId = sessionId; ActorId = actorId; Fingerprint = fingerprint; BookedCount = bookedCount; CreationTime = DateTime.UtcNow; }
}
