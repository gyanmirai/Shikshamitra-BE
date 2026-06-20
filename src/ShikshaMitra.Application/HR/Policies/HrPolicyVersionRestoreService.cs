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
		await holidaySnapshotService.ValidateHolidaysAsync(snapshot.AcademicYearId, snapshot.Holidays);
		var entity = new WorkCalendarPolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			snapshot.AcademicYearId,
			BuildRestoredName(input.Name, snapshot.Name, snapshot.Version),
			HrPolicyStatus.Draft,
			input.EffectiveFrom?.Date ?? snapshot.EffectiveFrom,
			snapshot.EffectiveTo,
			HrPolicyMapping.ParseWeeklyOffPattern(snapshot.WeeklyOffPattern),
			snapshot.CustomWeeklyOffs,
			HrPolicyMapping.ParseHolidaySyncMode(snapshot.HolidaySyncMode));
		await workCalendarRepository.InsertAsync(entity, autoSave: true);
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
		await workCalendarValidator.EnsureUsableAsync(snapshot.WorkCalendarPolicyId, snapshot.AcademicYearId, HrPolicyStatus.Draft);
		var entity = new PayrollPolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			snapshot.AcademicYearId,
			snapshot.WorkCalendarPolicyId,
			BuildRestoredName(input.Name, snapshot.Name, snapshot.Version),
			HrPolicyStatus.Draft,
			false,
			HrPolicyMapping.ParseDayCalculationType(snapshot.DayCalculationType),
			snapshot.ManualDays,
			HrPolicyMapping.ToManualMonths(snapshot.ManualMonthDays),
			HrPolicyMapping.ParseSaturdayWorkPolicy(snapshot.SaturdayWorkPolicy),
			HrPolicyMapping.ToHolidayOverrides(snapshot.HolidayOverrides));
		await payrollPolicyRepository.InsertAsync(entity, autoSave: true);
		await captureService.CaptureAsync("payroll", HrPolicyMapping.ToDto(entity), "restore-draft", input.ChangeReason);
		return entity;
	}

	private async Task<AttendancePolicy> RestoreAttendancePolicyAsync(HrPolicyVersion version, HrPolicyRestoreDraftRequestDto input)
	{
		var snapshot = Deserialize<AttendancePolicyDto>(version.SnapshotJson);
		EnsureSnapshotAcademicYearMatchesVersion(snapshot.AcademicYearId, version.AcademicYearId);
		await workCalendarValidator.EnsureUsableAsync(snapshot.WorkCalendarPolicyId, snapshot.AcademicYearId, HrPolicyStatus.Draft);
		var entity = new AttendancePolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			snapshot.AcademicYearId,
			snapshot.WorkCalendarPolicyId,
			BuildRestoredName(input.Name, snapshot.Name, snapshot.Version),
			HrPolicyStatus.Draft,
			false,
			HrPolicyMapping.ParseCaptureMode(snapshot.CaptureMode),
			snapshot.FullDayThresholdPercentage,
			snapshot.HalfDayThresholdPercentage,
			snapshot.AbsentThresholdPercentage,
			HrPolicyMapping.ParseMissingPunchBehavior(snapshot.MissingPunchBehavior),
			snapshot.ManualCorrectionAllowed,
			snapshot.GraceTime,
			snapshot.MaxLateAllowedPerMonth,
			HrPolicyMapping.ParsePenaltyType(snapshot.PenaltyType),
			snapshot.PenaltyCountThreshold,
			snapshot.EarlyExitEnabled,
			snapshot.EarlyExitGraceTime,
			snapshot.OvertimeEnabled,
			HrPolicyMapping.ParseOvertimeRate(snapshot.OvertimeRate),
			snapshot.MinOvertimeMinutes,
			HrPolicyMapping.ParseHolidayWorkBehavior(snapshot.HolidayWorkBehavior),
			snapshot.PayrollPreviewEnabled,
			snapshot.PenaltyDeductionAmount);
		await attendancePolicyRepository.InsertAsync(entity, autoSave: true);
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
