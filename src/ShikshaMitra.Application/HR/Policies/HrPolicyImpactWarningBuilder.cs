using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Policies;

internal static class HrPolicyImpactWarningBuilder
{
	public static void AddWorkCalendarReferenceWarning(HrPolicyImpactPreviewDto preview, WorkCalendarPolicyReferences refs)
	{
		if (preview.PolicyType != "work_calendar" || refs.IsEmpty)
		{
			return;
		}

		var referencedPolicies = new List<string>();
		if (refs.PayrollPolicyNames.Count > 0)
		{
			referencedPolicies.Add($"payroll ({refs.PayrollPolicyCount}): {string.Join(", ", refs.PayrollPolicyNames)}");
		}
		if (refs.AttendancePolicyNames.Count > 0)
		{
			referencedPolicies.Add($"attendance ({refs.AttendancePolicyCount}): {string.Join(", ", refs.AttendancePolicyNames)}");
		}

		preview.Warnings.Add(new HrPolicyImpactWarningDto
		{
			Severity = string.Equals(preview.Action, "archive", StringComparison.OrdinalIgnoreCase) ? "blocking" : "warning",
			Code = string.Equals(preview.Action, "archive", StringComparison.OrdinalIgnoreCase)
				? HRErrorCodes.WorkCalendarPolicyInUse
				: HRErrorCodes.WorkCalendarPolicyReferenced,
			Message = $"This work calendar is referenced by payroll or attendance policies ({string.Join("; ", referencedPolicies)}). Reassign or archive those policies before archiving this calendar."
		});
	}

	public static void AddWarnings(HrPolicyImpactPreviewDto preview)
	{
		if (preview.LockedPayrollMonths.Count > 0)
		{
			preview.Warnings.Add(new HrPolicyImpactWarningDto
			{
				Severity = "blocking",
				Code = HRErrorCodes.LockedPayrollImpact,
				Message = "This change affects locked payroll months. Review recalculation and audit requirements before saving."
			});
		}
		if (preview.AffectedEmployeeCount > 0)
		{
			preview.Warnings.Add(new HrPolicyImpactWarningDto
			{
				Severity = "info",
				Code = HRErrorCodes.AffectedEmployees,
				Message = $"{preview.AffectedEmployeeCount} employees may be affected."
			});
		}
	}
}
