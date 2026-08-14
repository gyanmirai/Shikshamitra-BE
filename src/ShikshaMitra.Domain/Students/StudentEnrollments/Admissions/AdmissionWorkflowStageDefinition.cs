using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowStageDefinition : Entity<Guid>
{
	public Guid WorkflowDefinitionId { get; private set; }
	public string Code { get; private set; }
	public string Name { get; private set; }
	public string? Description { get; private set; }
	public AdmissionWorkflowStageType StageType { get; private set; }
	public int DisplayOrder { get; private set; }
	public bool IsRequired { get; private set; }
	public bool AllowManualSkip { get; private set; }
	public string? ConfigurationJson { get; private set; }

	protected AdmissionWorkflowStageDefinition() { }

	public AdmissionWorkflowStageDefinition(Guid id, Guid workflowDefinitionId, string code, string name, AdmissionWorkflowStageType stageType, int displayOrder, bool isRequired, bool allowManualSkip, string? description, string? configurationJson) : base(id)
	{
		WorkflowDefinitionId = Check.NotDefaultOrNull<Guid>(workflowDefinitionId, nameof(workflowDefinitionId));
		Code = Check.NotNullOrWhiteSpace(code, nameof(code)).Trim().ToUpperInvariant();
		Name = Check.NotNullOrWhiteSpace(name, nameof(name)).Trim();
		StageType = stageType;
		DisplayOrder = displayOrder;
		IsRequired = isRequired;
		AllowManualSkip = allowManualSkip;
		Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
		ConfigurationJson = string.IsNullOrWhiteSpace(configurationJson) ? null : configurationJson.Trim();
	}
}
