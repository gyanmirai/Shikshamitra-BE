using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionApplicationWorkflowInstance : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; } public Guid ApplicationId { get; private set; } public Guid WorkflowDefinitionId { get; private set; } public DateTime StartedAt { get; private set; }
	protected AdmissionApplicationWorkflowInstance() { }
	public AdmissionApplicationWorkflowInstance(Guid id, Guid? tenantId, Guid applicationId, Guid workflowDefinitionId) : base(id) { TenantId = tenantId; ApplicationId = applicationId; WorkflowDefinitionId = workflowDefinitionId; StartedAt = DateTime.UtcNow; }
}

public class AdmissionApplicationStageInstance : Entity<Guid>
{
	public Guid WorkflowInstanceId { get; private set; } public Guid DefinitionStageId { get; private set; } public string Code { get; private set; } public string Name { get; private set; } public AdmissionWorkflowStageType StageType { get; private set; } public int DisplayOrder { get; private set; } public AdmissionRuntimeStageStatus Status { get; private set; } public DateTime? EnteredAt { get; private set; } public DateTime? CompletedAt { get; private set; }
	protected AdmissionApplicationStageInstance() { }
	public AdmissionApplicationStageInstance(Guid id, Guid workflowInstanceId, Guid definitionStageId, string code, string name, AdmissionWorkflowStageType stageType, int displayOrder, bool current) : base(id) { WorkflowInstanceId = workflowInstanceId; DefinitionStageId = definitionStageId; Code = code; Name = name; StageType = stageType; DisplayOrder = displayOrder; Status = current ? AdmissionRuntimeStageStatus.Current : AdmissionRuntimeStageStatus.Pending; if (current) EnteredAt = DateTime.UtcNow; }
	public void Complete() { Status = AdmissionRuntimeStageStatus.Completed; CompletedAt = DateTime.UtcNow; }
	public void Enter() { Status = AdmissionRuntimeStageStatus.Current; EnteredAt = DateTime.UtcNow; }
}

public class AdmissionFormInstance : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; } public Guid ApplicationId { get; private set; } public Guid FormDefinitionId { get; private set; } public int FormVersion { get; private set; } public DateTime StartedAt { get; private set; }
	protected AdmissionFormInstance() { }
	public AdmissionFormInstance(Guid id, Guid? tenantId, Guid applicationId, Guid formDefinitionId, int formVersion) : base(id) { TenantId = tenantId; ApplicationId = applicationId; FormDefinitionId = formDefinitionId; FormVersion = formVersion; StartedAt = DateTime.UtcNow; }
}

public class AdmissionFormFieldValue : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; } public Guid FormInstanceId { get; private set; } public string FieldCode { get; private set; } public string? ValueJson { get; private set; } public DateTime LastModifiedAt { get; private set; }
	protected AdmissionFormFieldValue() { }
	public AdmissionFormFieldValue(Guid id, Guid? tenantId, Guid formInstanceId, string fieldCode, string? valueJson) : base(id) { TenantId = tenantId; FormInstanceId = formInstanceId; FieldCode = fieldCode; SetValue(valueJson); }
	public void SetValue(string? valueJson) { ValueJson = valueJson; LastModifiedAt = DateTime.UtcNow; }
}

public class AdmissionActivity : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; } public Guid? ApplicantId { get; private set; } public Guid? ApplicationId { get; private set; } public Guid? InquiryId { get; private set; } public string ActivityType { get; private set; } public string Description { get; private set; } public DateTime OccurredAt { get; private set; } public string? MetadataJson { get; private set; }
	protected AdmissionActivity() { }
	public AdmissionActivity(Guid id, Guid? tenantId, string activityType, string description, Guid? applicantId = null, Guid? applicationId = null, Guid? inquiryId = null, string? metadataJson = null) : base(id) { TenantId = tenantId; ActivityType = activityType; Description = description; ApplicantId = applicantId; ApplicationId = applicationId; InquiryId = inquiryId; MetadataJson = metadataJson; OccurredAt = DateTime.UtcNow; }
}
