using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<IReadOnlyList<StudentGrNumberPreviewDto>> GetGrNumberPreviewsAsync(Guid academicYearId)
	{
		// Same year boundary as settings reads; counters stay under the current tenant filter.
		await LazyServiceProvider.LazyGetRequiredService<StudentScopeAccessService>()
			.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetGrNumberPreviewsAsync));
		var configuration = await configurationManager.GetAsync(academicYearId);
		if (configuration == null) return [];
		var year = await academicYearRepository.GetAsync(academicYearId);
		var rules = configuration.Rules("grNumber").Where(OnboardingRuleConfiguration.IsPublished).ToArray();
		var ids = rules.Select(rule => GrNumbers.GetSeriesId(configuration, rule, academicYearId)).Distinct().ToArray();
		// Continuous school counters may have been created in an earlier academic year.
		var counters = await seriesRepository.GetListAsync(series => Enumerable.Contains(ids, series.Id));
		return rules.Select(rule =>
		{
			var seriesId = GrNumbers.GetSeriesId(configuration, rule, academicYearId);
			var start = OnboardingRuleConfiguration.Number(rule, "startingNumber", 1);
			var next = Math.Max(counters.FirstOrDefault(series => series.Id == seriesId)?.NextSequence ?? start, start);
			var maximum = OnboardingRuleConfiguration.Number(rule, "maximumNumber");
			var exhausted = maximum > 0 && next > maximum;
			var effective = !DateTime.TryParse(OnboardingRuleConfiguration.Text(rule, "effectiveFrom"), out var from) || from.Date <= DateTime.UtcNow.Date;
			var (prefix, suffix) = StudentGrNumberAllocator.Format(rule, year.Name);
			var padding = (int)OnboardingRuleConfiguration.Number(rule, "padding", 6);
			return new StudentGrNumberPreviewDto
			{
				RuleId = OnboardingRuleConfiguration.Text(rule, "id"), NextSequence = next,
				MaximumNumber = maximum > 0 ? maximum : null, LimitReached = exhausted, IsEffective = effective,
				NextPreview = exhausted || !effective ? null : $"{prefix.Trim()}{next.ToString().PadLeft(padding, '0')}{suffix.Trim()}"
			};
		}).ToList();
	}
}
