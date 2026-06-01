using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.ProfessionalManagement;

public static class PerformanceScoreCalculator
{
	public static decimal CalculateFinalScore(
		IReadOnlyCollection<PerformanceTemplateSection> sections,
		IReadOnlyCollection<PerformanceTemplateCriterion> criteria,
		IReadOnlyCollection<PerformanceReviewResponse> responses,
		int ratingScaleMax)
	{
		if (sections.Count == 0 || criteria.Count == 0 || responses.Count == 0 || ratingScaleMax <= 0)
		{
			return 0m;
		}

		var responseByCriterion = responses
			.GroupBy(x => x.CriterionId)
			.ToDictionary(x => x.Key, x => x.Last());

		var total = 0m;
		foreach (var section in sections)
		{
			var sectionCriteria = criteria
				.Where(x => x.SectionId == section.Id)
				.Where(x => responseByCriterion.TryGetValue(x.Id, out var response) && !response.IsNotApplicable && response.Rating.HasValue)
				.ToList();

			var applicableWeight = sectionCriteria.Sum(x => x.Weight);
			if (applicableWeight <= 0)
			{
				continue;
			}

			var sectionScore = sectionCriteria.Sum(criterion =>
			{
				var rating = responseByCriterion[criterion.Id].Rating!.Value;
				var normalizedRating = Math.Clamp(rating, 0, ratingScaleMax) / (decimal)ratingScaleMax;
				return normalizedRating * (criterion.Weight / applicableWeight);
			});

			total += sectionScore * section.Weight;
		}

		return Math.Round(total, 2);
	}
}
