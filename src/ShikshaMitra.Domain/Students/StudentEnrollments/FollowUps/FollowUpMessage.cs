using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.FollowUps;

public class FollowUpMessage : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	// Foreign key to StudentEnrollment
	public Guid StudentEnrollmentId { get; set; }

	// Navigation property back to StudentEnrollment
	public virtual StudentEnrollment StudentEnrollment { get; set; }

	public string Message { get; set; }
	public string Author { get; set; }
	public DateTimeOffset Timestamp { get; set; }

	public Guid? TenantId { get; private set; }

	private FollowUpMessage() { } // EF Core needs a private constructor

	public FollowUpMessage(Guid id, Guid? tenantId, Guid studentEnrollmentId, string message, string author, DateTimeOffset timestamp)
		: base(id)
	{
		TenantId = tenantId;
		StudentEnrollmentId = studentEnrollmentId;
		Message = message;
		Author = author;
		Timestamp = timestamp;
	}
}