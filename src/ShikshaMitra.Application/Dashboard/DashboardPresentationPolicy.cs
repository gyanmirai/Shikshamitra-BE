using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Volo.Abp.Validation;

namespace ShikshaMitra.Dashboard;

public static class DashboardPresentationPolicy
{
	private static readonly Dictionary<string, string[]> Families = new(StringComparer.Ordinal)
	{
		["leadership"] = ["leadership"],
		["principalAdmin"] = ["principal", "admissions"],
		["teacher"] = ["teacher"],
		["financeHr"] = ["finance", "hr", "financeHr"],
		["studentParent"] = ["student", "parent"]
	};

	public static void Validate(string template, string? role)
	{
		if (!string.IsNullOrWhiteSpace(role) && (!Families.TryGetValue(template, out var roles) || !roles.Contains(role, StringComparer.Ordinal)))
			throw new AbpValidationException("Dashboard presentation is invalid.", [new ValidationResult("Presentation role must belong to the selected template.", [nameof(DashboardSummaryRequestDto.PresentationRole)])]);
	}

	public static string ResolveStaff(string template, string? requested, IEnumerable<string> currentRoles, bool finance, bool hr)
	{
		Validate(template, requested);
		if (!string.IsNullOrWhiteSpace(requested)) return requested;
		var roles = currentRoles.Select(x => x.Replace("_", " ").Replace("-", " ").ToLowerInvariant()).ToArray();
		return template switch
		{
			"principalAdmin" => roles.Any(x => x.Contains("admission", StringComparison.Ordinal) || x.Contains("counsellor", StringComparison.Ordinal)) ? "admissions" : "principal",
			"financeHr" => finance && hr ? "financeHr" : hr ? "hr" : "finance",
			_ => Families[template][0]
		};
	}
}
