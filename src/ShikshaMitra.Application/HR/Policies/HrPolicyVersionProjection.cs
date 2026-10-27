using System;
using System.Text.Json;

namespace ShikshaMitra.HR.Policies;

public static class HrPolicyVersionProjection
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static HrPolicyVersionDto ToDto(HrPolicyVersion entity) => new()
	{
		Id = entity.Id,
		PolicyType = entity.PolicyType,
		PolicyId = entity.PolicyId,
		AcademicYearId = entity.AcademicYearId,
		VersionNumber = entity.VersionNumber,
		Name = entity.Name,
		Status = entity.Status,
		EffectiveFrom = entity.EffectiveFrom,
		EffectiveTo = entity.EffectiveTo,
		ChangeAction = entity.ChangeAction,
		ChangeReason = entity.ChangeReason,
		CreationTime = entity.CreationTime,
		CreatorId = entity.CreatorId,
		Summary = BuildVersionSummary(entity),
		SnapshotJson = entity.SnapshotJson
	};

	public static string BuildVersionSummary(HrPolicyVersion entity) =>
		$"{HrPolicyVersionDiffBuilder.ToDisplayText(entity.ChangeAction)} v{entity.VersionNumber}";

	public static string NormalizePolicyType(string value) => value.Trim().ToLowerInvariant().Replace("-", "_") switch
	{
		"workcalendar" or "work_calendar" or "work-calendars" or "work_calendar_policies" => "work_calendar",
		"payroll" or "payroll_policy" => "payroll",
		"attendance" or "attendance_policy" => "attendance",
		_ => throw new Volo.Abp.BusinessException(HRErrorCodes.HrPolicyInvalidType)
	};

	public static string ToAuditPolicyEntityType(string normalizedPolicyType) => normalizedPolicyType switch
	{
		"work_calendar" => "work_calendar_policy",
		"payroll" => "payroll_policy",
		"attendance" => "attendance_policy",
		"leave" => "leave_policy",
		_ => "policy_version"
	};

	public static HrPolicyImpactPreviewRequestDto ToRestorePreviewRequest(
		string policyType,
		Guid policyId,
		HrPolicyVersion version)
	{
		var normalized = NormalizePolicyType(policyType);
		var request = new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = normalized,
			PolicyId = policyId,
			AcademicYearId = version.AcademicYearId,
			Action = "restore-draft"
		};

		switch (normalized)
		{
			case "work_calendar":
				request.WorkCalendarPolicy = ToRestoreWorkCalendarInput(version.SnapshotJson, version.AcademicYearId);
				break;
			case "payroll":
				request.PayrollPolicy = ToRestorePayrollInput(version.SnapshotJson, version.AcademicYearId);
				break;
			case "attendance":
				request.AttendancePolicy = ToRestoreAttendanceInput(version.SnapshotJson, version.AcademicYearId);
				break;
		}

		return request;
	}

	public static string NormalizeAuditAction(string action) => action.Trim().ToLowerInvariant().Replace("-", "_") switch
	{
		"restore_draft" => "restored_draft",
		var value => value
	};

	private static CreateUpdateWorkCalendarPolicyDto ToRestoreWorkCalendarInput(string snapshotJson, Guid versionAcademicYearId)
	{
		var snapshot = Deserialize<WorkCalendarPolicyDto>(snapshotJson);
		EnsureSnapshotAcademicYearMatchesVersion(snapshot.AcademicYearId, versionAcademicYearId);
		return new CreateUpdateWorkCalendarPolicyDto
		{
			AcademicYearId = snapshot.AcademicYearId,
			Name = snapshot.Name,
			Status = "draft",
			EffectiveFrom = snapshot.EffectiveFrom,
			EffectiveTo = snapshot.EffectiveTo,
			WeeklyOffPattern = snapshot.WeeklyOffPattern,
			CustomWeeklyOffs = snapshot.CustomWeeklyOffs,
			HolidaySyncMode = snapshot.HolidaySyncMode,
			Holidays = snapshot.Holidays
		};
	}

	private static CreateUpdatePayrollPolicyDto ToRestorePayrollInput(string snapshotJson, Guid versionAcademicYearId)
	{
		var snapshot = Deserialize<PayrollPolicyDto>(snapshotJson);
		EnsureSnapshotAcademicYearMatchesVersion(snapshot.AcademicYearId, versionAcademicYearId);
		return new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = snapshot.AcademicYearId,
			WorkCalendarPolicyId = snapshot.WorkCalendarPolicyId,
			Name = snapshot.Name,
			Status = "draft",
			IsDefault = false,
			DayCalculationType = snapshot.DayCalculationType,
			ManualDays = snapshot.ManualDays,
			ManualMonthDays = snapshot.ManualMonthDays,
			SaturdayWorkPolicy = snapshot.SaturdayWorkPolicy,
			HolidayOverrides = snapshot.HolidayOverrides
		};
	}

	private static CreateUpdateAttendancePolicyDto ToRestoreAttendanceInput(string snapshotJson, Guid versionAcademicYearId)
	{
		var snapshot = Deserialize<AttendancePolicyDto>(snapshotJson);
		EnsureSnapshotAcademicYearMatchesVersion(snapshot.AcademicYearId, versionAcademicYearId);
		return new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = snapshot.AcademicYearId,
			WorkCalendarPolicyId = snapshot.WorkCalendarPolicyId,
			Name = snapshot.Name,
			Status = "draft",
			IsDefault = false,
			CaptureMode = snapshot.CaptureMode,
			FullDayThresholdPercentage = snapshot.FullDayThresholdPercentage,
			HalfDayThresholdPercentage = snapshot.HalfDayThresholdPercentage,
			AbsentThresholdPercentage = snapshot.AbsentThresholdPercentage,
			MissingPunchBehavior = snapshot.MissingPunchBehavior,
			ManualCorrectionAllowed = snapshot.ManualCorrectionAllowed,
			GraceTime = snapshot.GraceTime,
			MaxLateAllowedPerMonth = snapshot.MaxLateAllowedPerMonth,
			PenaltyType = snapshot.PenaltyType,
			PenaltyCountThreshold = snapshot.PenaltyCountThreshold,
			PenaltyDeductionAmount = snapshot.PenaltyDeductionAmount,
			EarlyExitEnabled = snapshot.EarlyExitEnabled,
			EarlyExitGraceTime = snapshot.EarlyExitGraceTime,
			OvertimeEnabled = snapshot.OvertimeEnabled,
			OvertimeRate = snapshot.OvertimeRate,
			MinOvertimeMinutes = snapshot.MinOvertimeMinutes,
			HolidayWorkBehavior = snapshot.HolidayWorkBehavior,
			PayrollPreviewEnabled = snapshot.PayrollPreviewEnabled
		};
	}

	private static T Deserialize<T>(string snapshotJson)
	{
		try
		{
			return JsonSerializer.Deserialize<T>(snapshotJson, JsonOptions) ??
				throw new Volo.Abp.BusinessException(HRErrorCodes.HrPolicyInvalidVersionSnapshot);
		}
		catch (JsonException ex)
		{
			throw new Volo.Abp.BusinessException(HRErrorCodes.HrPolicyInvalidVersionSnapshot, innerException: ex);
		}
	}

	private static void EnsureSnapshotAcademicYearMatchesVersion(Guid snapshotAcademicYearId, Guid versionAcademicYearId)
	{
		if (snapshotAcademicYearId != versionAcademicYearId)
		{
			throw new Volo.Abp.BusinessException(HRErrorCodes.HrPolicyAssignmentAcademicYearMismatch);
		}
	}
}
