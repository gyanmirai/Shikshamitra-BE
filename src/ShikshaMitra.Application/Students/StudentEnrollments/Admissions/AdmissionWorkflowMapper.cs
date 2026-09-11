using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionWorkflowMapper
{
	public static AdmissionWorkflowDto ToDto(
		AdmissionWorkflowDefinition workflow,
		IEnumerable<AdmissionWorkflowStageDefinition>? stages = null,
		IEnumerable<AdmissionWorkflowTransitionDefinition>? transitions = null)
	{
		return new AdmissionWorkflowDto
		{
			Id = workflow.Id,
			Name = workflow.Name,
			Code = workflow.Code,
			Description = workflow.Description,
			Version = workflow.Version,
			Status = workflow.Status,
			ParentVersionId = workflow.ParentVersionId,
			PublishedAt = workflow.PublishedAt,
			PublishedBy = workflow.PublishedBy,
			ConcurrencyStamp = workflow.ConcurrencyStamp,
			CreationTime = workflow.CreationTime,
			CreatorId = workflow.CreatorId,
			LastModificationTime = workflow.LastModificationTime,
			LastModifierId = workflow.LastModifierId,
			Stages = (stages ?? []).Select(ToDto).ToList(),
			Transitions = (transitions ?? []).Select(ToDto).ToList()
		};
	}

	public static AdmissionWorkflowValidationIssueDto ToDto(AdmissionWorkflowValidationIssue issue) => new()
	{
		Code = issue.Code,
		Message = issue.Message,
		Severity = issue.Severity,
		StageId = issue.StageId,
		TransitionId = issue.TransitionId
	};

	private static AdmissionWorkflowStageDto ToDto(AdmissionWorkflowStageDefinition stage) => new()
	{
		Id = stage.Id,
		WorkflowDefinitionId = stage.WorkflowDefinitionId,
		Code = stage.Code,
		Name = stage.Name,
		Description = stage.Description,
		StageType = stage.StageType,
		DisplayOrder = stage.DisplayOrder,
		IsRequired = stage.IsRequired,
		AllowManualSkip = stage.AllowManualSkip,
		IsEntryStage = stage.IsEntryStage,
		IsTerminalStage = stage.IsTerminalStage,
		ConfigurationJson = stage.ConfigurationJson
	};

	private static AdmissionWorkflowTransitionDto ToDto(AdmissionWorkflowTransitionDefinition transition) => new()
	{
		Id = transition.Id,
		WorkflowDefinitionId = transition.WorkflowDefinitionId,
		FromStageId = transition.FromStageId,
		ToStageId = transition.ToStageId,
		Priority = transition.Priority,
		ConditionRuleJson = transition.ConditionRuleJson,
		IsDefault = transition.IsDefault
	};
}
