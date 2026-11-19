using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentSessionScope : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid SessionId { get; private set; }
	public Guid AdmissionOfferingId { get; private set; }
	public Guid WorkflowStageDefinitionId { get; private set; }
	protected AdmissionAssessmentSessionScope() { }
	public AdmissionAssessmentSessionScope(Guid id, Guid? tenantId, Guid sessionId, Guid offeringId, Guid stageId) : base(id)
	{
		TenantId = tenantId; SessionId = sessionId; AdmissionOfferingId = offeringId; WorkflowStageDefinitionId = stageId;
	}
}
