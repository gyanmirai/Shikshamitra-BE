using System;
using System.Collections.Generic;
using System.Linq;

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

		var isArchive = string.Equals(preview.Action, "archive", StringComparison.OrdinalIgnoreCase);
		var guidance = isArchive
			? "Reassign or archive those policies before archiving this calendar."
			: "Reassign those policies before changing this calendar away from active.";

		preview.Warnings.Add(new HrPolicyImpactWarningDto
		{
			Severity = isArchive ? "blocking" : "warning",
			Code = isArchive
				? HRErrorCodes.WorkCalendarPolicyInUse
				: HRErrorCodes.WorkCalendarPolicyReferenced,
			Message = $"This work calendar is referenced by payroll or attendance policies ({string.Join("; ", referencedPolicies)}). {guidance}"
		});
	}

	public static void AddWorkCalendarDeactivationWarning(
		HrPolicyImpactPreviewDto preview,
		WorkCalendarPolicyReferences refs,
		HrPolicyStatus proposedStatus)
	{
		if (preview.PolicyType != "work_calendar" ||
			refs.IsEmpty ||
			!string.Equals(preview.Action, "update", StringComparison.OrdinalIgnoreCase) ||
			proposedStatus == HrPolicyStatus.Active)
		{
			return;
		}

		preview.Warnings.Add(new HrPolicyImpactWarningDto
		{
			Severity = "blocking",
			Code = HRErrorCodes.WorkCalendarPolicyMustBeActive,
			Message = "This work calendar is referenced by active payroll or attendance policies. Reassign those policies before changing this calendar away from active."
		});
	}

	public static void AddWarnings(HrPolicyImpactPreviewDto preview)
	{
		AddLockedPayrollWarning(preview);
		if (preview.AffectedEmployeeCount > 0)
		{
			preview.Warnings.Add(new HrPolicyImpactWarningDto
			{
				Severity = "info",
				Code = HRErrorCodes.AffectedEmployees,
				Message = $"{preview.AffectedEmployeeCount} staff may be affected."
			});
		}
	}

	public static void AddLockedPayrollWarning(HrPolicyImpactPreviewDto preview)
	{
		if (preview.LockedPayrollMonths.Count == 0)
		{
			return;
		}

		preview.Warnings.Add(new HrPolicyImpactWarningDto
		{
			Severity = "blocking",
			Code = HRErrorCodes.LockedPayrollImpact,
			Message = "This change affects locked payroll months. Review recalculation and audit requirements before saving."
		});
	}

	public static void AddDefaultReplacementWarning(HrPolicyImpactPreviewDto preview, string existingDefaultName)
	{
		preview.Warnings.Add(new HrPolicyImpactWarningDto
		{
			Severity = "warning",
			Code = HRErrorCodes.DefaultPolicyWillBeReplaced,
			Message = $"Saving this policy as the default will replace the current default \"{existingDefaultName}\". Review affected staff before confirming."
		});
	}

	public static void AddWorkCalendarStatusWarning(
		HrPolicyImpactPreviewDto preview,
		WorkCalendarPolicy? workCalendar,
		HrPolicyStatus? proposedPolicyStatus = null)
	{
		if (workCalendar == null || workCalendar.Status == HrPolicyStatus.Active)
		{
			return;
		}

		var isBlocking = workCalendar.Status == HrPolicyStatus.Archived || proposedPolicyStatus == HrPolicyStatus.Active;
		preview.Warnings.Add(new HrPolicyImpactWarningDto
		{
			Severity = isBlocking ? "blocking" : "warning",
			Code = workCalendar.Status == HrPolicyStatus.Archived
				? HRErrorCodes.HrPolicyInvalidWorkCalendar
				: HRErrorCodes.WorkCalendarPolicyMustBeActive,
			Message = $"The referenced work calendar \"{workCalendar.Name}\" is {workCalendar.Status.ToString().ToLowerInvariant()}. Use an active calendar before assigning this policy broadly."
		});
	}

	public static void AddMismatchedWorkCalendarWarning(HrPolicyImpactPreviewDto preview, WorkCalendarPolicy workCalendar)
	{
		preview.Warnings.Add(new HrPolicyImpactWarningDto
		{
			Severity = "blocking",
			Code = HRErrorCodes.HrPolicyInvalidWorkCalendar,
			Message = $"The referenced work calendar \"{workCalendar.Name}\" belongs to a different academic year. Select an active calendar from this academic year before assigning this policy broadly."
		});
	}

	public static void AddMissingWorkCalendarWarning(HrPolicyImpactPreviewDto preview, Guid workCalendarPolicyId)
	{
		preview.Warnings.Add(new HrPolicyImpactWarningDto
		{
			Severity = "blocking",
			Code = HRErrorCodes.HrPolicyInvalidWorkCalendar,
			Message = $"The referenced work calendar \"{workCalendarPolicyId:D}\" could not be found. Select an active calendar before assigning this policy broadly."
		});
	}

	public static void FinalizeSummary(HrPolicyImpactPreviewDto preview)
	{
		preview.BlockingWarnings = GetBlockingWarnings(preview);
		var confirmableNoticeCount = preview.Warnings.Count(RequiresConfirmation);

		preview.RiskSummary = preview.BlockingWarnings.Count > 0
			? $"{preview.BlockingWarnings.Count} blocking issue(s) must be resolved before this action can continue."
			: preview.RequiresConfirmation
				? $"{confirmableNoticeCount} policy notice(s) should be reviewed before confirming this action."
				: preview.Warnings.Count > 0
					? $"{preview.Warnings.Count} non-blocking notice(s) found; no confirmation is required."
				: "No policy risks detected for this action.";

		preview.ResolutionSummary = preview.AffectedEmployeeCount > 0
			? $"{preview.AffectedEmployeeCount} staff members and {preview.AffectedAssignmentCount} assignment(s) may be affected by this {preview.PolicyType.Replace("_", " ")} {preview.Action}."
			: $"No staff are currently affected by this {preview.PolicyType.Replace("_", " ")} {preview.Action}.";

		preview.RecommendedAction = preview.BlockingWarnings.Count > 0
			? "Resolve the blocking warnings, then run impact preview again."
			: preview.RequiresConfirmation || preview.Warnings.Count > 0
				? preview.RequiresConfirmation
					? "Review the policy notices and affected staff before confirming."
					: "This action can be saved after reviewing the informational notices."
				: "This action can be saved after normal review.";
	}

	internal static bool RequiresConfirmation(HrPolicyImpactWarningDto warning) =>
		warning.Code != HRErrorCodes.HrPolicyNoAffectedEmployees &&
		(string.Equals(warning.Severity, "warning", StringComparison.OrdinalIgnoreCase) ||
		 string.Equals(warning.Severity, "info", StringComparison.OrdinalIgnoreCase));

	internal static List<HrPolicyImpactWarningDto> GetBlockingWarnings(HrPolicyImpactPreviewDto preview) =>
		preview.Warnings
			.Concat(preview.BlockingWarnings)
			.Where(x => string.Equals(x.Severity, "blocking", StringComparison.OrdinalIgnoreCase))
			.GroupBy(x => $"{x.Code}\u001f{x.Message}", StringComparer.Ordinal)
			.Select(x => x.First())
			.ToList();
}
