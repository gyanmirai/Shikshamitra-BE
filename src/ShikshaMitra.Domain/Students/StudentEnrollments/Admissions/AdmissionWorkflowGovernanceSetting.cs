using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowGovernanceSetting : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public bool MakerCheckerEnabled { get; private set; }
	public bool InAppNotificationsEnabled { get; private set; }
	public bool EmailNotificationsEnabled { get; private set; }

	private AdmissionWorkflowGovernanceSetting() { }

	public AdmissionWorkflowGovernanceSetting(Guid id, Guid? tenantId) : base(id)
	{
		TenantId = tenantId;
		InAppNotificationsEnabled = true;
		EmailNotificationsEnabled = true;
	}

	public void Update(bool makerCheckerEnabled, bool inAppNotificationsEnabled, bool emailNotificationsEnabled)
	{
		MakerCheckerEnabled = makerCheckerEnabled;
		InAppNotificationsEnabled = inAppNotificationsEnabled;
		EmailNotificationsEnabled = emailNotificationsEnabled;
	}
}
