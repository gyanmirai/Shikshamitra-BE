using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Audit;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
public class HrPolicyVersionRestoreService(
	IRepository<WorkCalendarPolicy, Guid> workCalendarRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	WorkCalendarHolidaySnapshotService holidaySnapshotService,
	HrPolicyWorkCalendarValidator workCalendarValidator,
	HrPolicyNameUniquenessService nameUniquenessService,
	HrPolicyVersionCaptureService captureService,
	HrAuditWriter auditWriter)
	: ApplicationService, ITransientDependency
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public async Task<object> RestoreDraftAsync(HrPolicyVersion version, string policyType, Guid policyId, HrPolicyRestoreDraftRequestDto input)
	{
		var normalized = HrPolicyVersionProjection.NormalizePolicyType(policyType);
		var restored = normalized switch
		{
			"work_calendar" => (object)await RestoreWorkCalendarAsync(version, input),
			"payroll" => HrPolicyMapping.ToDto(await RestorePayrollPolicyAsync(version, input)),
			"attendance" => HrPolicyMapping.ToDto(await RestoreAttendancePolicyAsync(version, input)),
			_ => throw new BusinessException(HRErrorCodes.HrPolicyInvalidType)
		};

		await auditWriter.WriteAsync(
			version.AcademicYearId,
			HrPolicyVersionProjection.ToAuditPolicyEntityType(normalized),
			policyId,
			version.Name,
			"restored_draft",
			$"Restored {normalized} policy version {version.VersionNumber} as draft.",
			input.ChangeReason,
			context: new { sourceVersionId = version.Id, sourceVersionNumber = version.VersionNumber });
		return restored;
	}

	private async Task<WorkCalendarPolicyDto> RestoreWorkCalendarAsync(HrPolicyVersion version, HrPolicyRestoreDraftRequestDto input)
	{
		var snapshot = Deserialize<WorkCalendarPolicyDto>(version.SnapshotJson);
		EnsureSnapshotAcademicYearMatchesVersion(snapshot.AcademicYearId, version.AcademicYearId);
		var weeklyOffPattern = HrPolicyMapping.ParseWeeklyOffPattern(snapshot.WeeklyOffPattern);
		var holidaySyncMode = HrPolicyMapping.ParseHolidaySyncMode(snapshot.HolidaySyncMode);
		await holidaySnapshotService.ValidateHolidaysAsync(snapshot.AcademicYearId, snapshot.Holidays);
		var restoredName = BuildRestoredName(input.Name, snapshot.Name, snapshot.Version);
		await nameUniquenessService.EnsureWorkCalendarAvailableAsync(snapshot.AcademicYearId, restoredName);
		var entity = new WorkCalendarPolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			snapshot.AcademicYearId,
			restoredName,
			HrPolicyStatus.Draft,
			input.EffectiveFrom?.Date ?? snapshot.EffectiveFrom,
			snapshot.EffectiveTo,
			weeklyOffPattern,
			snapshot.CustomWeeklyOffs,
			holidaySyncMode);
		await nameUniquenessService.ExecuteWorkCalendarWriteAsync(
			entity.Name,
			() => workCalendarRepository.InsertAsync(entity, autoSave: true));
		await holidaySnapshotService.ReplaceHolidaysAsync(entity.Id, snapshot.AcademicYearId, snapshot.Holidays);
		var dto = HrPolicyMapping.ToDto(entity);
		dto.Holidays = await holidaySnapshotService.GetHolidaysAsync(entity.Id);
		dto.HolidaySummary = HrPolicyMapping.BuildHolidaySummary(dto.Holidays);
		await captureService.CaptureAsync("work_calendar", dto, "restore-draft", input.ChangeReason);
		return dto;
	}

	private async Task<PayrollPolicy> RestorePayrollPolicyAsync(HrPolicyVersion version, HrPolicyRestoreDraftRequestDto input)
	{
		var snapshot = Deserialize<PayrollPolicyDto>(version.SnapshotJson);
		EnsureSnapshotAcademicYearMatchesVersion(snapshot.AcademicYearId, version.AcademicYearId);
		var dayCalculationType = HrPolicyMapping.ParseDayCalculationType(snapshot.DayCalculationType);
		var manualMonthDays = HrPolicyMapping.ToManualMonths(snapshot.ManualMonthDays);
		var saturdayWorkPolicy = HrPolicyMapping.ParseSaturdayWorkPolicy(snapshot.SaturdayWorkPolicy);
		var holidayOverrides = HrPolicyMapping.ToHolidayOverrides(snapshot.HolidayOverrides);
		await workCalendarValidator.EnsureUsableAsync(snapshot.WorkCalendarPolicyId, snapshot.AcademicYearId, HrPolicyStatus.Draft);
		var restoredName = BuildRestoredName(input.Name, snapshot.Name, snapshot.Version);
		await nameUniquenessService.EnsurePayrollAvailableAsync(snapshot.AcademicYearId, restoredName);
		var entity = new PayrollPolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			snapshot.AcademicYearId,
			snapshot.WorkCalendarPolicyId,
			restoredName,
			HrPolicyStatus.Draft,
			false,
			dayCalculationType,
			snapshot.ManualDays,
			manualMonthDays,
			saturdayWorkPolicy,
			holidayOverrides);
		await nameUniquenessService.ExecutePayrollWriteAsync(
			entity.Name,
			() => payrollPolicyRepository.InsertAsync(entity, autoSave: true));
		await captureService.CaptureAsync("payroll", HrPolicyMapping.ToDto(entity), "restore-draft", input.ChangeReason);
		return entity;
	}

	private async Task<AttendancePolicy> RestoreAttendancePolicyAsync(HrPolicyVersion version, HrPolicyRestoreDraftRequestDto input)
	{
		var snapshot = Deserialize<AttendancePolicyDto>(version.SnapshotJson);
		EnsureSnapshotAcademicYearMatchesVersion(snapshot.AcademicYearId, version.AcademicYearId);
		var captureMode = HrPolicyMapping.ParseCaptureMode(snapshot.CaptureMode);
		var missingPunchBehavior = HrPolicyMapping.ParseMissingPunchBehavior(snapshot.MissingPunchBehavior);
		var penaltyType = HrPolicyMapping.ParsePenaltyType(snapshot.PenaltyType);
		var overtimeRate = HrPolicyMapping.ParseOvertimeRate(snapshot.OvertimeRate);
		var holidayWorkBehavior = HrPolicyMapping.ParseHolidayWorkBehavior(snapshot.HolidayWorkBehavior);
		await workCalendarValidator.EnsureUsableAsync(snapshot.WorkCalendarPolicyId, snapshot.AcademicYearId, HrPolicyStatus.Draft);
		var restoredName = BuildRestoredName(input.Name, snapshot.Name, snapshot.Version);
		await nameUniquenessService.EnsureAttendanceAvailableAsync(snapshot.AcademicYearId, restoredName);
		var entity = new AttendancePolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			snapshot.AcademicYearId,
			snapshot.WorkCalendarPolicyId,
			restoredName,
			HrPolicyStatus.Draft,
			false,
			captureMode,
			snapshot.FullDayThresholdPercentage,
			snapshot.HalfDayThresholdPercentage,
			snapshot.AbsentThresholdPercentage,
			missingPunchBehavior,
			snapshot.ManualCorrectionAllowed,
			snapshot.GraceTime,
			snapshot.MaxLateAllowedPerMonth,
			penaltyType,
			snapshot.PenaltyCountThreshold,
			snapshot.EarlyExitEnabled,
			snapshot.EarlyExitGraceTime,
			snapshot.OvertimeEnabled,
			overtimeRate,
			snapshot.MinOvertimeMinutes,
			holidayWorkBehavior,
			snapshot.PayrollPreviewEnabled,
			snapshot.PenaltyDeductionAmount);
		await nameUniquenessService.ExecuteAttendanceWriteAsync(
			entity.Name,
			() => attendancePolicyRepository.InsertAsync(entity, autoSave: true));
		await captureService.CaptureAsync("attendance", HrPolicyMapping.ToDto(entity), "restore-draft", input.ChangeReason);
		return entity;
	}

	private static T Deserialize<T>(string json)
	{
		try
		{
			return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw InvalidSnapshot();
		}
		catch (JsonException ex) { throw InvalidSnapshot(ex); }
	}
	private static BusinessException InvalidSnapshot(Exception? ex = null) =>
		new(HRErrorCodes.HrPolicyInvalidVersionSnapshot, innerException: ex);

	private static string BuildRestoredName(string? requestedName, string sourceName, int version) =>
		string.IsNullOrWhiteSpace(requestedName) ? $"{sourceName} v{version} Restore Draft" : requestedName.Trim();

	private static void EnsureSnapshotAcademicYearMatchesVersion(Guid snapshotAcademicYearId, Guid versionAcademicYearId)
	{
		if (snapshotAcademicYearId != versionAcademicYearId) { throw new BusinessException(HRErrorCodes.HrPolicyAssignmentAcademicYearMismatch); }
	}
}
