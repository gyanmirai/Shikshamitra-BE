using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Employees.Onboarding;

internal static class EmployeeApplicationDateRules
{
	public static void ValidateWorkExperience(
		DateTime? startDate,
		DateTime? endDate,
		DateOnly currentDate,
		string path,
		List<ValidationResult> results)
	{
		if (!startDate.HasValue)
		{
			Add(results, "Start date is required.", $"{path}.startDate");
		}
		else if (DateOnly.FromDateTime(startDate.Value) > currentDate)
		{
			Add(results, "Start date cannot be in the future.", $"{path}.startDate");
		}

		if (endDate.HasValue &&
		    DateOnly.FromDateTime(endDate.Value) > currentDate)
		{
			Add(results, "End date cannot be in the future.", $"{path}.endDate");
		}

		if (startDate.HasValue &&
		    endDate.HasValue &&
		    endDate.Value.Date < startDate.Value.Date)
		{
			Add(results, "End date must be on or after start date.", $"{path}.endDate");
		}
	}

	private static void Add(
		List<ValidationResult> results,
		string message,
		string memberName) =>
		results.Add(new ValidationResult(message, [memberName]));
}
