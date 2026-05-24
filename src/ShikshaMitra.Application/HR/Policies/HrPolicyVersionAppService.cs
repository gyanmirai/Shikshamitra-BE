using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Audit;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
[Authorize]
public class HrPolicyVersionAppService(
	IRepository<HrPolicyVersion, Guid> versionRepository,
	IRepository<WorkCalendarPolicy, Guid> workCalendarRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	HrAuditWriter auditWriter)
	: ApplicationService, IHrPolicyVersionAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = false
	};

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<PagedResultDto<HrPolicyVersionDto>> GetListAsync(string policyType, Guid policyId, PagedAndSortedResultRequestDto input)
	{
		var normalized = NormalizePolicyType(policyType);
		var query = await versionRepository.GetQueryableAsync();
		query = query.Where(x => x.PolicyType == normalized && x.PolicyId == policyId);
		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query
			.OrderByDescending(x => x.VersionNumber)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount <= 0 ? 20 : input.MaxResultCount));
		return new PagedResultDto<HrPolicyVersionDto>(total, rows.Select(ToDto).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<HrPolicyVersionDto> GetAsync(string policyType, Guid policyId, Guid versionId)
	{
		var normalized = NormalizePolicyType(policyType);
		var entity = await versionRepository.GetAsync(versionId);
		if (entity.PolicyType != normalized || entity.PolicyId != policyId)
		{
			throw new BusinessException("ShikshaMitra:PolicyVersionNotFound");
		}
		return ToDto(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<HrPolicyVersionCompareDto> CompareAsync(string policyType, Guid policyId, Guid fromVersionId, Guid toVersionId)
	{
		var from = await GetVersionEntityAsync(policyType, policyId, fromVersionId);
		var to = await GetVersionEntityAsync(policyType, policyId, toVersionId);
		return new HrPolicyVersionCompareDto
		{
			PolicyType = NormalizePolicyType(policyType),
			PolicyId = policyId,
			FromVersion = ToDto(from),
			ToVersion = ToDto(to),
			Differences = BuildDifferences(from.SnapshotJson, to.SnapshotJson)
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Clone)]
	public async Task<object> RestoreDraftAsync(string policyType, Guid policyId, Guid versionId, HrPolicyRestoreDraftRequestDto input)
	{
		var version = await GetVersionEntityAsync(policyType, policyId, versionId);
		var normalized = NormalizePolicyType(policyType);
		var restored = normalized switch
		{
			"work_calendar" => (object)HrPolicyMapping.ToDto(await RestoreWorkCalendarAsync(version, input)),
			"payroll" => HrPolicyMapping.ToDto(await RestorePayrollPolicyAsync(version, input)),
			"attendance" => HrPolicyMapping.ToDto(await RestoreAttendancePolicyAsync(version, input)),
			_ => throw new BusinessException("ShikshaMitra:InvalidPolicyType")
		};
		await auditWriter.WriteAsync(
			version.AcademicYearId,
			ToAuditPolicyEntityType(normalized),
			policyId,
			version.Name,
			"restored_draft",
			$"Restored {normalized} policy version {version.VersionNumber} as draft.",
			input.ChangeReason,
			context: new { sourceVersionId = version.Id, sourceVersionNumber = version.VersionNumber });
		return restored;
	}

	public async Task CaptureAsync(string policyType, object policyDto, string action, string? reason = null)
	{
		var normalized = NormalizePolicyType(policyType);
		var snapshotJson = JsonSerializer.Serialize(policyDto, JsonOptions);
		var version = normalized switch
		{
			"work_calendar" => BuildWorkCalendarVersion((WorkCalendarPolicyDto)policyDto, snapshotJson, action, reason),
			"payroll" => BuildPayrollVersion((PayrollPolicyDto)policyDto, snapshotJson, action, reason),
			"attendance" => BuildAttendanceVersion((AttendancePolicyDto)policyDto, snapshotJson, action, reason),
			_ => throw new BusinessException("ShikshaMitra:InvalidPolicyType")
		};

		var query = await versionRepository.GetQueryableAsync();
		var exists = await AsyncExecuter.AnyAsync(query.Where(x =>
			x.PolicyType == version.PolicyType &&
			x.PolicyId == version.PolicyId &&
			x.VersionNumber == version.VersionNumber));
		if (!exists)
		{
			await versionRepository.InsertAsync(version, autoSave: true);
			await auditWriter.WriteAsync(
				version.AcademicYearId,
				"policy_version",
				version.Id,
				$"{version.Name} v{version.VersionNumber}",
				NormalizeAuditAction(action),
				$"Captured {version.PolicyType} policy version {version.VersionNumber}.",
				reason,
				context: new
				{
					version.PolicyType,
					version.PolicyId,
					version.VersionNumber,
					version.Status,
					version.EffectiveFrom,
					version.EffectiveTo
				});
		}
	}

	private async Task<HrPolicyVersion> GetVersionEntityAsync(string policyType, Guid policyId, Guid versionId)
	{
		var normalized = NormalizePolicyType(policyType);
		var entity = await versionRepository.GetAsync(versionId);
		if (entity.PolicyType != normalized || entity.PolicyId != policyId)
		{
			throw new BusinessException("ShikshaMitra:PolicyVersionNotFound");
		}
		return entity;
	}

	private async Task<WorkCalendarPolicy> RestoreWorkCalendarAsync(HrPolicyVersion version, HrPolicyRestoreDraftRequestDto input)
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
		await CaptureAsync("work_calendar", HrPolicyMapping.ToDto(entity), "restore-draft", input.ChangeReason);
		return entity;
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
		await CaptureAsync("payroll", HrPolicyMapping.ToDto(entity), "restore-draft", input.ChangeReason);
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
		await CaptureAsync("attendance", HrPolicyMapping.ToDto(entity), "restore-draft", input.ChangeReason);
		return entity;
	}

	private HrPolicyVersion BuildWorkCalendarVersion(WorkCalendarPolicyDto dto, string snapshotJson, string action, string? reason) =>
		new(GuidGenerator.Create(), CurrentTenant.Id, "work_calendar", dto.Id, dto.AcademicYearId, dto.Version, dto.Name, dto.Status, dto.EffectiveFrom, dto.EffectiveTo, snapshotJson, action, reason);

	private HrPolicyVersion BuildPayrollVersion(PayrollPolicyDto dto, string snapshotJson, string action, string? reason) =>
		new(GuidGenerator.Create(), CurrentTenant.Id, "payroll", dto.Id, dto.AcademicYearId, dto.Version, dto.Name, dto.Status, null, null, snapshotJson, action, reason);

	private HrPolicyVersion BuildAttendanceVersion(AttendancePolicyDto dto, string snapshotJson, string action, string? reason) =>
		new(GuidGenerator.Create(), CurrentTenant.Id, "attendance", dto.Id, dto.AcademicYearId, dto.Version, dto.Name, dto.Status, null, null, snapshotJson, action, reason);

	private static HrPolicyVersionDto ToDto(HrPolicyVersion entity) => new()
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
		SnapshotJson = entity.SnapshotJson
	};

	private static List<HrPolicyVersionDifferenceDto> BuildDifferences(string fromJson, string toJson)
	{
		using var fromDoc = JsonDocument.Parse(fromJson);
		using var toDoc = JsonDocument.Parse(toJson);
		var fromProps = fromDoc.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetRawText());
		var toProps = toDoc.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetRawText());
		return fromProps.Keys.Union(toProps.Keys)
			.Where(field => !string.Equals(fromProps.GetValueOrDefault(field), toProps.GetValueOrDefault(field), StringComparison.Ordinal))
			.Select(field => new HrPolicyVersionDifferenceDto
			{
				Field = field,
				FromValue = TrimJsonValue(fromProps.GetValueOrDefault(field)),
				ToValue = TrimJsonValue(toProps.GetValueOrDefault(field))
			})
			.OrderBy(x => x.Field)
			.ToList();
	}

	private static T Deserialize<T>(string json) =>
		JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new BusinessException("ShikshaMitra:InvalidPolicyVersionSnapshot");

	private static string BuildRestoredName(string? requestedName, string sourceName, int version) =>
		string.IsNullOrWhiteSpace(requestedName) ? $"{sourceName} v{version} Restore Draft" : requestedName.Trim();

	private static string? TrimJsonValue(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return value;
		}
		return value.Length > 500 ? value[..500] + "..." : value.Trim('"');
	}

	private static string NormalizePolicyType(string value) => value.Trim().ToLowerInvariant().Replace("-", "_") switch
	{
		"workcalendar" or "work_calendar" or "work-calendars" or "work_calendar_policies" => "work_calendar",
		"payroll" or "payroll_policy" => "payroll",
		"attendance" or "attendance_policy" => "attendance",
		_ => throw new BusinessException("ShikshaMitra:InvalidPolicyType")
	};

	private static string ToAuditPolicyEntityType(string normalizedPolicyType) => normalizedPolicyType switch
	{
		"work_calendar" => "work_calendar_policy",
		"payroll" => "payroll_policy",
		"attendance" => "attendance_policy",
		_ => "policy_version"
	};

	private static string NormalizeAuditAction(string action) => action.Trim().ToLowerInvariant().Replace("-", "_") switch
	{
		"restore_draft" => "restored_draft",
		var value => value
	};
}
