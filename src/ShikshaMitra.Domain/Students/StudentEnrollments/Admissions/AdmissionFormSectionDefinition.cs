using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
namespace ShikshaMitra.Students.StudentEnrollments.Admissions;
public class AdmissionFormSectionDefinition : Entity<Guid>
{
	public Guid FormDefinitionId { get; private set; } public string Code { get; private set; } public string Name { get; private set; } public int DisplayOrder { get; private set; } public string? VisibilityRuleJson { get; private set; }
	public Guid? StepId { get; private set; } public string? StepCode { get; private set; } public string? StepName { get; private set; } public int? StepDisplayOrder { get; private set; } public string? StepVisibilityRuleJson { get; private set; }
	protected AdmissionFormSectionDefinition() { }
	public AdmissionFormSectionDefinition(Guid id, Guid formDefinitionId, string code, string name, int displayOrder, string? visibilityRuleJson,
		Guid? stepId = null, string? stepCode = null, string? stepName = null, int? stepDisplayOrder = null, string? stepVisibilityRuleJson = null) : base(id)
	{
		FormDefinitionId = Check.NotDefaultOrNull<Guid>(formDefinitionId, nameof(formDefinitionId));
		Code = Check.NotNullOrWhiteSpace(code, nameof(code)).Trim().ToUpperInvariant();
		Name = Check.NotNullOrWhiteSpace(name, nameof(name)).Trim();
		DisplayOrder = displayOrder;
		VisibilityRuleJson = string.IsNullOrWhiteSpace(visibilityRuleJson) ? null : visibilityRuleJson.Trim();
		StepId = stepId;
		StepCode = string.IsNullOrWhiteSpace(stepCode) ? null : stepCode.Trim().ToUpperInvariant();
		StepName = string.IsNullOrWhiteSpace(stepName) ? null : stepName.Trim();
		StepDisplayOrder = stepDisplayOrder;
		StepVisibilityRuleJson = string.IsNullOrWhiteSpace(stepVisibilityRuleJson) ? null : stepVisibilityRuleJson.Trim();
	}
}
