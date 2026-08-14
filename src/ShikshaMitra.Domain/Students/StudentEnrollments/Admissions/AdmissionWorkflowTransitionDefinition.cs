using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowTransitionDefinition : Entity<Guid>
{
	public Guid WorkflowDefinitionId { get; private set; }
	public Guid FromStageId { get; private set; }
	public Guid ToStageId { get; private set; }
	public int Priority { get; private set; }

	protected AdmissionWorkflowTransitionDefinition() { }

	public AdmissionWorkflowTransitionDefinition(Guid id, Guid workflowDefinitionId, Guid fromStageId, Guid toStageId, int priority) : base(id)
	{
		WorkflowDefinitionId = Check.NotDefaultOrNull<Guid>(workflowDefinitionId, nameof(workflowDefinitionId));
		FromStageId = Check.NotDefaultOrNull<Guid>(fromStageId, nameof(fromStageId));
		ToStageId = Check.NotDefaultOrNull<Guid>(toStageId, nameof(toStageId));
		Priority = priority;
	}
}
