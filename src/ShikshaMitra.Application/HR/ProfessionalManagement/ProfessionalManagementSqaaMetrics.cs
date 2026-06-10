using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.ProfessionalManagement;

internal static class ProfessionalManagementSqaaMetrics
{
	public static IEnumerable<PerformanceSqaaDomain> GetDomains(PerformanceSqaaDomain? sqaaDomain)
	{
		if (sqaaDomain.HasValue)
		{
			yield return sqaaDomain.Value;
			yield break;
		}

		foreach (var domain in Enum.GetValues(typeof(PerformanceSqaaDomain)).Cast<PerformanceSqaaDomain>())
		{
			yield return domain;
		}
	}

	public static int CalculateReadinessPercentage(int submittedEvidence, int requiredEvidence) =>
		requiredEvidence <= 0
			? 0
			: (int)Math.Round((decimal)submittedEvidence * 100m / requiredEvidence, MidpointRounding.AwayFromZero);
}
