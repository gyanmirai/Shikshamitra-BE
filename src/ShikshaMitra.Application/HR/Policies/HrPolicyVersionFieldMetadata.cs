namespace ShikshaMitra.HR.Policies;

internal static class HrPolicyVersionFieldMetadata
{
	public static string FieldLabel(string field) => field switch
	{
		"workCalendarPolicyId" => "Work calendar",
		"dayCalculationType" => "Day calculation",
		"manualMonthDays" => "Manual month days",
		"holidayOverrides" => "Holiday payroll rules",
		"fullDayThresholdPercentage" => "Full day threshold",
		"halfDayThresholdPercentage" => "Half day threshold",
		"absentThresholdPercentage" => "Absent threshold",
		"missingPunchBehavior" => "Missing punch behavior",
		"manualCorrectionAllowed" => "Manual correction",
		"graceTime" => "Grace time",
		"maxLateAllowedPerMonth" => "Max late allowed",
		"penaltyType" => "Penalty type",
		"penaltyCountThreshold" => "Penalty threshold",
		"earlyExitEnabled" => "Early exit",
		"earlyExitGraceTime" => "Early exit grace time",
		"overtimeEnabled" => "Overtime",
		"overtimeRate" => "Overtime rate",
		"minOvertimeMinutes" => "Minimum overtime minutes",
		"holidayWorkBehavior" => "Holiday work behavior",
		"sandwichRuleEnabled" => "Sandwich rule",
		"payrollPreviewEnabled" => "Payroll preview",
		"weeklyOffPattern" => "Weekly off pattern",
		"customWeeklyOffs" => "Custom weekly offs",
		"holidaySyncMode" => "Holiday sync mode",
		"holidays" => "Holidays",
		"effectiveFrom" => "Effective from",
		"effectiveTo" => "Effective to",
		"isDefault" => "Default policy",
		"version" => "Version",
		"name" => "Policy name",
		"status" => "Status",
		"academicYearId" => "Academic year",
		"creatorId" => "Created by",
		"lastModifierId" => "Last modified by",
		"creationTime" => "Created",
		"lastModificationTime" => "Last modified",
		_ => HrPolicyVersionDiffBuilder.ToDisplayText(field)
	};

	public static string FieldCategory(string field) => field switch
	{
		"name" or "status" or "isDefault" or "version" or "academicYearId" or "creatorId" or "lastModifierId" or "creationTime" or "lastModificationTime" => "Basic info",
		"effectiveFrom" or "effectiveTo" => "Effective dates",
		"dayCalculationType" or "manualDays" or "manualMonthDays" or "saturdayWorkPolicy" or "holidayOverrides" => "Payroll rules",
		"fullDayThresholdPercentage" or "halfDayThresholdPercentage" or "absentThresholdPercentage" or "missingPunchBehavior" or "manualCorrectionAllowed" or "graceTime" or "maxLateAllowedPerMonth" or "penaltyType" or "penaltyCountThreshold" or "earlyExitEnabled" or "earlyExitGraceTime" or "overtimeEnabled" or "overtimeRate" or "minOvertimeMinutes" or "holidayWorkBehavior" or "sandwichRuleEnabled" or "payrollPreviewEnabled" => "Attendance rules",
		"weeklyOffPattern" or "customWeeklyOffs" or "holidaySyncMode" or "holidays" or "holidaySummary" => "Holiday rules",
		"workCalendarPolicyId" => "Assignment/impact references",
		_ => "Other changes"
	};

	public static string DayName(int day) => day switch
	{
		0 => "Sunday",
		1 => "Monday",
		2 => "Tuesday",
		3 => "Wednesday",
		4 => "Thursday",
		5 => "Friday",
		6 => "Saturday",
		_ => $"Day {day}"
	};
}
