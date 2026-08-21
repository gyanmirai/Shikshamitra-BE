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
	public string? ConditionRuleJson { get; private set; }
	public bool IsDefault { get; private set; }

	protected AdmissionWorkflowTransitionDefinition() { }

	public AdmissionWorkflowTransitionDefinition(
		Guid id,
		Guid workflowDefinitionId,
		Guid fromStageId,
		Guid toStageId,
		int priority,
		string? conditionRuleJson = null,
		bool isDefault = false) : base(id)
	{
		WorkflowDefinitionId = Check.NotDefaultOrNull<Guid>(workflowDefinitionId, nameof(workflowDefinitionId));
		SetDetails(fromStageId, toStageId, priority, conditionRuleJson, isDefault);
	}

	public void Update(Guid fromStageId, Guid toStageId, int priority, string? conditionRuleJson, bool isDefault)
	{
		SetDetails(fromStageId, toStageId, priority, conditionRuleJson, isDefault);
	}

	private void SetDetails(Guid fromStageId, Guid toStageId, int priority, string? conditionRuleJson, bool isDefault)
	{
		FromStageId = Check.NotDefaultOrNull<Guid>(fromStageId, nameof(fromStageId));
		ToStageId = Check.NotDefaultOrNull<Guid>(toStageId, nameof(toStageId));
		Priority = priority;
		ConditionRuleJson = string.IsNullOrWhiteSpace(conditionRuleJson)
			? null
			: Check.Length(conditionRuleJson.Trim(), nameof(conditionRuleJson), AdmissionWorkflowConsts.MaxConditionRuleLength);
		IsDefault = isDefault;
	}
}
