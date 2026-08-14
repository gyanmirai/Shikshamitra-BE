using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
namespace ShikshaMitra.Students.StudentEnrollments.Admissions;
public class AdmissionFormSectionDefinition : Entity<Guid>
{
	public Guid FormDefinitionId { get; private set; } public string Code { get; private set; } public string Name { get; private set; } public int DisplayOrder { get; private set; } public string? VisibilityRuleJson { get; private set; }
	protected AdmissionFormSectionDefinition() { }
	public AdmissionFormSectionDefinition(Guid id, Guid formDefinitionId, string code, string name, int displayOrder, string? visibilityRuleJson) : base(id) { FormDefinitionId = Check.NotDefaultOrNull<Guid>(formDefinitionId, nameof(formDefinitionId)); Code = Check.NotNullOrWhiteSpace(code, nameof(code)).Trim().ToUpperInvariant(); Name = Check.NotNullOrWhiteSpace(name, nameof(name)).Trim(); DisplayOrder = displayOrder; VisibilityRuleJson = string.IsNullOrWhiteSpace(visibilityRuleJson) ? null : visibilityRuleJson.Trim(); }
}
