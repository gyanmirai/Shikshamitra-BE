using System;
using System.Linq;
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
	IRepository<WorkCalendarHoliday, Guid> holidaySnapshotRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	HrPolicyVersionCaptureService captureService,
	HrAuditWriter auditWriter)
	: ApplicationService, ITransientDependency
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = false
	};

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
		await RestoreWorkCalendarHolidaysAsync(entity.Id, snapshot);
		var dto = HrPolicyMapping.ToDto(entity);
		dto.Holidays = await GetWorkCalendarHolidaysAsync(entity.Id);
		dto.HolidaySummary = HrPolicyMapping.BuildHolidaySummary(dto.Holidays);
		await captureService.CaptureAsync("work_calendar", dto, "restore-draft", input.ChangeReason);
		return dto;
	}

	private async Task RestoreWorkCalendarHolidaysAsync(Guid restoredPolicyId, WorkCalendarPolicyDto snapshot)
	{
		if (snapshot.Holidays.Count == 0)
		{
			return;
		}

		foreach (var row in snapshot.Holidays)
		{
			var fromDate = (row.FromDate ?? row.Date).Date;
			var toDate = (row.ToDate ?? row.Date).Date;
			if (toDate < fromDate)
			{
				toDate = fromDate;
			}

			var holiday = new WorkCalendarHoliday(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				snapshot.AcademicYearId,
				restoredPolicyId,
				fromDate,
				toDate,
				row.HolidayName,
				HrPolicyMapping.ParseWorkCalendarHolidaySource(row.Source),
				row.CalendarHolidayId,
				row.IsIncluded,
				row.Notes);
			await holidaySnapshotRepository.InsertAsync(holiday, autoSave: false);
		}

		if (UnitOfWorkManager.Current is not null)
		{
			await UnitOfWorkManager.Current.SaveChangesAsync();
		}
	}

	private async Task<System.Collections.Generic.List<WorkCalendarHolidayDto>> GetWorkCalendarHolidaysAsync(Guid policyId)
	{
		var query = await holidaySnapshotRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(query
			.Where(x => x.WorkCalendarPolicyId == policyId)
			.OrderBy(x => x.FromDate)
			.ThenBy(x => x.HolidayName));
		return rows.Select(HrPolicyMapping.ToDto).ToList();
	}

	private async Task<PayrollPolicy> RestorePayrollPolicyAsync(HrPolicyVersion version, HrPolicyRestoreDraftRequestDto input)
	{
		var snapshot = Deserialize<PayrollPolicyDto>(version.SnapshotJson);
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
		var entity = new AttendancePolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			snapshot.AcademicYearId,
			snapshot.WorkCalendarPolicyId,
			BuildRestoredName(input.Name, snapshot.Name, snapshot.Version),
			HrPolicyStatus.Draft,
			false,
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
			snapshot.SandwichRuleEnabled,
			snapshot.PayrollPreviewEnabled);
		await attendancePolicyRepository.InsertAsync(entity, autoSave: true);
		await captureService.CaptureAsync("attendance", HrPolicyMapping.ToDto(entity), "restore-draft", input.ChangeReason);
		return entity;
	}

	private static T Deserialize<T>(string json) =>
		JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new BusinessException(HRErrorCodes.HrPolicyInvalidVersionSnapshot);

	private static string BuildRestoredName(string? requestedName, string sourceName, int version) =>
		string.IsNullOrWhiteSpace(requestedName) ? $"{sourceName} v{version} Restore Draft" : requestedName.Trim();
}
