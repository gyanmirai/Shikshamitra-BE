using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class InquiryPreference : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string PreferenceName { get; private set; }

	protected InquiryPreference() { } // For ORM

	internal InquiryPreference(Guid id, Guid? tenantId, string preferenceName) : base(id)
	{
		TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
		PreferenceName = preferenceName;
	}
}