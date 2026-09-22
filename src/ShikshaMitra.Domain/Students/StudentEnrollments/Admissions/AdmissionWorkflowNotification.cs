using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowNotification : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid WorkflowDefinitionId { get; private set; }
	public Guid RecipientUserId { get; private set; }
	public AdmissionWorkflowNotificationType Type { get; private set; }
	public string Title { get; private set; } = string.Empty;
	public string Message { get; private set; } = string.Empty;
	public bool IsRead { get; private set; }
	public DateTime? ReadAt { get; private set; }

	private AdmissionWorkflowNotification() { }

	public AdmissionWorkflowNotification(Guid id, Guid? tenantId, Guid workflowDefinitionId,
		Guid recipientUserId, AdmissionWorkflowNotificationType type, string title, string message) : base(id)
	{
		TenantId = tenantId;
		WorkflowDefinitionId = Check.NotDefaultOrNull<Guid>(workflowDefinitionId, nameof(workflowDefinitionId));
		RecipientUserId = Check.NotDefaultOrNull<Guid>(recipientUserId, nameof(recipientUserId));
		Type = type;
		Title = Check.NotNullOrWhiteSpace(title, nameof(title), AdmissionWorkflowConsts.MaxNotificationTitleLength).Trim();
		Message = Check.NotNullOrWhiteSpace(message, nameof(message), AdmissionWorkflowConsts.MaxNotificationMessageLength).Trim();
	}

	public void MarkRead(DateTime readAt)
	{
		IsRead = true;
		ReadAt = readAt.ToUniversalTime();
	}
}
