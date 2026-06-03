using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.ProfessionalManagement;

public class PerformanceTemplateManager : DomainService
{
	public virtual void Publish(
		PerformanceTemplate template,
		IReadOnlyCollection<PerformanceTemplateSection> sections,
		IReadOnlyCollection<PerformanceTemplateCriterion> criteria)
	{
		template.Publish(
			sections.Sum(x => x.Weight),
			ProfessionalManagementRules.HasValidRubric(sections, criteria));
	}
}
