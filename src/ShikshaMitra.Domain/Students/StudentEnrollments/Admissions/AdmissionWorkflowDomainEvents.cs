using System;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionWorkflowCreatedEvent(Guid WorkflowDefinitionId, Guid? TenantId);
public sealed record AdmissionWorkflowPublishedEvent(Guid WorkflowDefinitionId, int Version, Guid? TenantId);
public sealed record AdmissionWorkflowVersionCreatedEvent(Guid SourceWorkflowDefinitionId, Guid NewWorkflowDefinitionId, int Version, Guid? TenantId);
public sealed record AdmissionWorkflowArchivedEvent(Guid WorkflowDefinitionId, int Version, Guid? TenantId);

public sealed record ApplicationWorkflowStartedEvent(Guid ApplicationId, Guid WorkflowInstanceId, Guid WorkflowDefinitionId, Guid EntryStageInstanceId, Guid? TenantId);
public sealed record ApplicationWorkflowStageEnteredEvent(Guid ApplicationId, Guid WorkflowInstanceId, Guid StageInstanceId, Guid? TenantId);
public sealed record ApplicationWorkflowStageCompletedEvent(Guid ApplicationId, Guid WorkflowInstanceId, Guid StageInstanceId, Guid? TenantId);
public sealed record ApplicationWorkflowStageSkippedEvent(Guid ApplicationId, Guid WorkflowInstanceId, Guid StageInstanceId, string Reason, Guid? TenantId);
public sealed record ApplicationWorkflowTransitionedEvent(Guid ApplicationId, Guid WorkflowInstanceId, Guid FromStageInstanceId, Guid ToStageInstanceId, Guid? TransitionDefinitionId, Guid? TenantId);
public sealed record ApplicationWorkflowCompletedEvent(Guid ApplicationId, Guid WorkflowInstanceId, Guid TerminalStageInstanceId, Guid? TenantId);
