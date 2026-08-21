using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowStageDefinition : Entity<Guid>
{
	public Guid WorkflowDefinitionId { get; private set; }
	public string Code { get; private set; } = string.Empty;
	public string Name { get; private set; } = string.Empty;
	public string? Description { get; private set; }
	public AdmissionWorkflowStageType StageType { get; private set; }
	public int DisplayOrder { get; private set; }
	public bool IsRequired { get; private set; }
	public bool AllowManualSkip { get; private set; }
	public bool IsEntryStage { get; private set; }
	public bool IsTerminalStage { get; private set; }
	public string? ConfigurationJson { get; private set; }

	protected AdmissionWorkflowStageDefinition() { }

	public AdmissionWorkflowStageDefinition(
		Guid id,
		Guid workflowDefinitionId,
		string code,
		string name,
		AdmissionWorkflowStageType stageType,
		int displayOrder,
		bool isRequired,
		bool allowManualSkip,
		string? description,
		string? configurationJson,
		bool isEntryStage = false,
		bool isTerminalStage = false) : base(id)
	{
		WorkflowDefinitionId = Check.NotDefaultOrNull<Guid>(workflowDefinitionId, nameof(workflowDefinitionId));
		SetDetails(code, name, stageType, displayOrder, isRequired, allowManualSkip, description, configurationJson, isEntryStage, isTerminalStage);
	}

	public void Update(
		string code,
		string name,
		AdmissionWorkflowStageType stageType,
		int displayOrder,
		bool isRequired,
		bool allowManualSkip,
		string? description,
		string? configurationJson,
		bool isEntryStage,
		bool isTerminalStage)
	{
		SetDetails(code, name, stageType, displayOrder, isRequired, allowManualSkip, description, configurationJson, isEntryStage, isTerminalStage);
	}

	private void SetDetails(
		string code,
		string name,
		AdmissionWorkflowStageType stageType,
		int displayOrder,
		bool isRequired,
		bool allowManualSkip,
		string? description,
		string? configurationJson,
		bool isEntryStage,
		bool isTerminalStage)
	{
		Code = Check.NotNullOrWhiteSpace(code, nameof(code), AdmissionWorkflowConsts.MaxCodeLength).Trim().ToUpperInvariant();
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), AdmissionWorkflowConsts.MaxNameLength).Trim();
		StageType = stageType;
		DisplayOrder = displayOrder;
		IsRequired = isRequired;
		AllowManualSkip = allowManualSkip;
		IsEntryStage = isEntryStage;
		IsTerminalStage = isTerminalStage;
		Description = string.IsNullOrWhiteSpace(description)
			? null
			: Check.Length(description.Trim(), nameof(description), AdmissionWorkflowConsts.MaxStageDescriptionLength);
		ConfigurationJson = string.IsNullOrWhiteSpace(configurationJson)
			? null
			: Check.Length(configurationJson.Trim(), nameof(configurationJson), AdmissionWorkflowConsts.MaxConfigurationLength);
	}
}
