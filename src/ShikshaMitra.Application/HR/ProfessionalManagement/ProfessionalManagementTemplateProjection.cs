using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.ProfessionalManagement;

internal static class ProfessionalManagementTemplateProjection
{
	public static PerformanceTemplateDto ToTemplateDto(
		PerformanceTemplate template,
		IReadOnlyCollection<PerformanceTemplateSection> sections,
		IReadOnlyCollection<PerformanceTemplateCriterion> criteria) =>
		new()
		{
			Id = template.Id,
			Name = template.Name,
			StaffCategory = template.StaffCategory,
			RoleName = template.RoleName,
			RatingScaleMax = template.RatingScaleMax,
			AllowNotApplicable = template.AllowNotApplicable,
			Status = template.Status,
			Description = template.Description,
			Sections = sections.Select(s => new PerformanceTemplateSectionDto
			{
				Id = s.Id,
				TemplateId = s.TemplateId,
				Name = s.Name,
				Weight = s.Weight,
				DisplayOrder = s.DisplayOrder,
				Criteria = criteria.Where(c => c.SectionId == s.Id).Select(ToCriterionDto).ToList()
			}).ToList()
		};

	private static PerformanceTemplateCriterionDto ToCriterionDto(PerformanceTemplateCriterion criterion) =>
		new()
		{
			Id = criterion.Id,
			TemplateId = criterion.TemplateId,
			SectionId = criterion.SectionId,
			Name = criterion.Name,
			Description = criterion.Description,
			Weight = criterion.Weight,
			EvidenceRequired = criterion.EvidenceRequired,
			DisplayOrder = criterion.DisplayOrder
		};
}
