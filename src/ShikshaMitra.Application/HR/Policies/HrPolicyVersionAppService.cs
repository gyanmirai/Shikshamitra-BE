using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Audit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
[Authorize]
public class HrPolicyVersionAppService(
	IRepository<HrPolicyVersion, Guid> versionRepository,
	IRepository<WorkCalendarPolicy, Guid> workCalendarRepository,
	IRepository<WorkCalendarHoliday, Guid> holidaySnapshotRepository,
	IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
	IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
	IRepository<IdentityUser, Guid> userRepository,
	IHrPolicySafetyAppService policySafetyAppService,
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
		var dtos = rows.Select(ToDto).ToList();
		await HydrateVersionCreatorsAsync(dtos);
		return new PagedResultDto<HrPolicyVersionDto>(total, dtos);
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
		var dto = ToDto(entity);
		await HydrateVersionCreatorsAsync(new List<HrPolicyVersionDto> { dto });
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<HrPolicyVersionCompareDto> CompareAsync(string policyType, Guid policyId, Guid fromVersionId, Guid toVersionId)
	{
		var from = await GetVersionEntityAsync(policyType, policyId, fromVersionId);
		var to = await GetVersionEntityAsync(policyType, policyId, toVersionId);
		var displayContext = await BuildDisplayContextAsync(from.SnapshotJson, to.SnapshotJson);
		var fromDto = ToDto(from);
		var toDto = ToDto(to);
		await HydrateVersionCreatorsAsync(new List<HrPolicyVersionDto> { fromDto, toDto });
		return new HrPolicyVersionCompareDto
		{
			PolicyType = NormalizePolicyType(policyType),
			PolicyId = policyId,
			FromVersion = fromDto,
			ToVersion = toDto,
			Differences = BuildDifferences(from.SnapshotJson, to.SnapshotJson, displayContext)
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.PreviewImpact)]
	public async Task<HrPolicyImpactPreviewDto> GetRestorePreviewAsync(string policyType, Guid policyId, Guid versionId)
	{
		var version = await GetVersionEntityAsync(policyType, policyId, versionId);
		return await policySafetyAppService.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = NormalizePolicyType(policyType),
			PolicyId = policyId,
			AcademicYearId = version.AcademicYearId,
			Action = "restore-draft"
		});
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Clone)]
	public async Task<object> RestoreDraftAsync(string policyType, Guid policyId, Guid versionId, HrPolicyRestoreDraftRequestDto input)
	{
		var version = await GetVersionEntityAsync(policyType, policyId, versionId);
		var normalized = NormalizePolicyType(policyType);
		var restored = normalized switch
		{
			"work_calendar" => (object)await RestoreWorkCalendarAsync(version, input),
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
			"work_calendar" => BuildWorkCalendarVersion((WorkCalendarPolicyDto) policyDto, snapshotJson, action, reason),
			"payroll" => BuildPayrollVersion((PayrollPolicyDto) policyDto, snapshotJson, action, reason),
			"attendance" => BuildAttendanceVersion((AttendancePolicyDto) policyDto, snapshotJson, action, reason),
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
		await CaptureAsync("work_calendar", dto, "restore-draft", input.ChangeReason);
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

	private async Task<List<WorkCalendarHolidayDto>> GetWorkCalendarHolidaysAsync(Guid policyId)
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
		Summary = BuildVersionSummary(entity),
		SnapshotJson = entity.SnapshotJson
	};

	private async Task HydrateVersionCreatorsAsync(List<HrPolicyVersionDto> versions)
	{
		var creatorIds = versions
			.Select(x => x.CreatorId)
			.Where(x => x.HasValue)
			.Select(x => x!.Value)
			.Distinct()
			.ToList();
		if (creatorIds.Count == 0)
		{
			return;
		}

		var users = await AsyncExecuter.ToListAsync((await userRepository.GetQueryableAsync()).Where(x => creatorIds.Contains(x.Id)));
		var namesById = users.ToDictionary(x => x.Id, x => string.IsNullOrWhiteSpace(x.Name) ? x.UserName : x.Name);
		foreach (var version in versions)
		{
			if (version.CreatorId.HasValue)
			{
				version.CreatedByName = namesById.GetValueOrDefault(version.CreatorId.Value) ?? "Unknown user";
			}
		}
	}

	private static List<HrPolicyVersionDifferenceDto> BuildDifferences(string fromJson, string toJson, VersionDisplayContext displayContext)
	{
		using var fromDoc = JsonDocument.Parse(fromJson);
		using var toDoc = JsonDocument.Parse(toJson);
		var fromProps = fromDoc.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone());
		var toProps = toDoc.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone());
		return fromProps.Keys.Union(toProps.Keys)
			.Where(field => !JsonValuesEqual(fromProps.GetValueOrDefault(field), toProps.GetValueOrDefault(field)))
			.Select(field => new HrPolicyVersionDifferenceDto
			{
				Field = field,
				FieldKey = field,
				FieldLabel = FieldLabel(field),
				Category = FieldCategory(field),
				FromRawValue = GetRawValue(fromProps.GetValueOrDefault(field)),
				ToRawValue = GetRawValue(toProps.GetValueOrDefault(field)),
				FromDisplayValue = FormatDisplayValue(field, fromProps.GetValueOrDefault(field), displayContext),
				ToDisplayValue = FormatDisplayValue(field, toProps.GetValueOrDefault(field), displayContext),
				FromValue = FormatDisplayValue(field, fromProps.GetValueOrDefault(field), displayContext),
				ToValue = FormatDisplayValue(field, toProps.GetValueOrDefault(field), displayContext)
			})
			.OrderBy(x => x.Category)
			.ThenBy(x => x.FieldLabel)
			.ToList();
	}

	private async Task<VersionDisplayContext> BuildDisplayContextAsync(params string[] snapshots)
	{
		var ids = new HashSet<Guid>();
		foreach (var snapshot in snapshots.Where(x => !string.IsNullOrWhiteSpace(x)))
		{
			using var doc = JsonDocument.Parse(snapshot);
			CollectGuids(doc.RootElement, ids);
		}

		var context = new VersionDisplayContext();
		if (ids.Count == 0)
		{
			return context;
		}

		var workCalendars = await AsyncExecuter.ToListAsync((await workCalendarRepository.GetQueryableAsync()).Where(x => ids.Contains(x.Id)));
		foreach (var item in workCalendars)
		{
			context.PolicyNames[item.Id] = item.Name;
		}

		var payrollPolicies = await AsyncExecuter.ToListAsync((await payrollPolicyRepository.GetQueryableAsync()).Where(x => ids.Contains(x.Id)));
		foreach (var item in payrollPolicies)
		{
			context.PolicyNames[item.Id] = item.Name;
		}

		var attendancePolicies = await AsyncExecuter.ToListAsync((await attendancePolicyRepository.GetQueryableAsync()).Where(x => ids.Contains(x.Id)));
		foreach (var item in attendancePolicies)
		{
			context.PolicyNames[item.Id] = item.Name;
		}

		var users = await AsyncExecuter.ToListAsync((await userRepository.GetQueryableAsync()).Where(x => ids.Contains(x.Id)));
		foreach (var user in users)
		{
			context.UserNames[user.Id] = string.IsNullOrWhiteSpace(user.Name) ? user.UserName : user.Name;
		}

		return context;
	}

	private static void CollectGuids(JsonElement element, HashSet<Guid> ids)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.String:
				if (Guid.TryParse(element.GetString(), out var id))
				{
					ids.Add(id);
				}
				break;
			case JsonValueKind.Object:
				foreach (var property in element.EnumerateObject())
				{
					CollectGuids(property.Value, ids);
				}
				break;
			case JsonValueKind.Array:
				foreach (var item in element.EnumerateArray())
				{
					CollectGuids(item, ids);
				}
				break;
		}
	}

	private static bool JsonValuesEqual(JsonElement left, JsonElement right) =>
		string.Equals(GetRawValue(left), GetRawValue(right), StringComparison.Ordinal);

	private static string? GetRawValue(JsonElement value) =>
		value.ValueKind == JsonValueKind.Undefined ? null : value.GetRawText();

	private static string FormatDisplayValue(string field, JsonElement value, VersionDisplayContext displayContext)
	{
		if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
		{
			return "Not set";
		}

		if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
		{
			return value.GetBoolean() ? "Enabled" : "Disabled";
		}

		if (value.ValueKind == JsonValueKind.String)
		{
			var text = value.GetString();
			if (string.IsNullOrWhiteSpace(text))
			{
				return "Not set";
			}
			if (IsDateDisplayField(field) && TryFormatVersionDateTime(text, out var formattedDate))
			{
				return formattedDate;
			}
			if (Guid.TryParse(text, out var id))
			{
				return ResolveGuidDisplay(field, id, displayContext);
			}
			return ToDisplayText(text);
		}

		if (value.ValueKind == JsonValueKind.Number)
		{
			return value.GetRawText();
		}

		if (value.ValueKind == JsonValueKind.Array)
		{
			return FormatArrayValue(field, value, displayContext);
		}

		if (value.ValueKind == JsonValueKind.Object)
		{
			return FormatObjectValue(field, value, displayContext);
		}

		return "Not set";
	}

	private static string ResolveGuidDisplay(string field, Guid id, VersionDisplayContext displayContext)
	{
		if (field.EndsWith("creatorId", StringComparison.OrdinalIgnoreCase) ||
			field.EndsWith("modifierId", StringComparison.OrdinalIgnoreCase))
		{
			return displayContext.UserNames.GetValueOrDefault(id) ?? "Unknown user";
		}

		return displayContext.PolicyNames.GetValueOrDefault(id) ?? "Unknown policy";
	}

	private static bool IsDateDisplayField(string field) =>
		field.Equals("creationTime", StringComparison.OrdinalIgnoreCase) ||
		field.Equals("lastModificationTime", StringComparison.OrdinalIgnoreCase) ||
		field.Equals("effectiveFrom", StringComparison.OrdinalIgnoreCase) ||
		field.Equals("effectiveTo", StringComparison.OrdinalIgnoreCase);

	private static bool TryFormatVersionDateTime(string value, out string formatted)
	{
		var normalized = value.Trim();
		if (normalized.Length >= 9 &&
			char.IsDigit(normalized[0]) &&
			char.IsDigit(normalized[1]) &&
			char.IsDigit(normalized[2]) &&
			char.IsDigit(normalized[3]) &&
			char.IsDigit(normalized[4]) &&
			char.IsDigit(normalized[5]) &&
			char.IsDigit(normalized[6]) &&
			char.IsDigit(normalized[7]) &&
			normalized[8] == 'T')
		{
			normalized = $"{normalized[..4]}-{normalized[4..6]}-{normalized[6..8]}{normalized[8..]}";
		}

		if (DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
		{
			formatted = parsed.ToString("dd MMM yyyy, hh:mm tt", CultureInfo.InvariantCulture);
			return true;
		}

		formatted = string.Empty;
		return false;
	}

	private static string FormatArrayValue(string field, JsonElement value, VersionDisplayContext displayContext)
	{
		var items = value.EnumerateArray().ToList();
		if (items.Count == 0)
		{
			return "None";
		}

		return field switch
		{
			"manualMonthDays" => string.Join(", ", items.Select(FormatManualMonth)),
			"holidayOverrides" => $"{items.Count} holiday rule{(items.Count == 1 ? string.Empty : "s")}",
			"holidays" => $"{items.Count} holiday{(items.Count == 1 ? string.Empty : "s")}",
			"customWeeklyOffs" => string.Join(", ", items.Select(x => DayName(x.GetInt32()))),
			_ => string.Join(", ", items.Take(3).Select(item => FormatDisplayValue(field, item, displayContext))) + (items.Count > 3 ? $" +{items.Count - 3} more" : string.Empty)
		};
	}

	private static string FormatObjectValue(string field, JsonElement value, VersionDisplayContext displayContext)
	{
		if (Guid.TryParse(value.ToString(), out var id))
		{
			return ResolveGuidDisplay(field, id, displayContext);
		}
		return "Changed";
	}

	private static string FormatManualMonth(JsonElement value)
	{
		if (value.ValueKind != JsonValueKind.Object)
		{
			return ToDisplayText(value.ToString());
		}
		var month = value.TryGetProperty("month", out var monthValue) ? monthValue.GetInt32() : 0;
		var year = value.TryGetProperty("year", out var yearValue) ? yearValue.GetInt32() : 0;
		var days = value.TryGetProperty("manualDays", out var daysValue) ? daysValue.GetInt32() : 0;
		return month > 0 && year > 0 ? $"{year}-{month:00}: {days} days" : $"{days} days";
	}

	private static string FieldLabel(string field) => field switch
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
		_ => ToDisplayText(field)
	};

	private static string FieldCategory(string field) => field switch
	{
		"name" or "status" or "isDefault" or "version" or "academicYearId" or "creatorId" or "lastModifierId" or "creationTime" or "lastModificationTime" => "Basic info",
		"effectiveFrom" or "effectiveTo" => "Effective dates",
		"dayCalculationType" or "manualDays" or "manualMonthDays" or "saturdayWorkPolicy" or "holidayOverrides" => "Payroll rules",
		"fullDayThresholdPercentage" or "halfDayThresholdPercentage" or "absentThresholdPercentage" or "missingPunchBehavior" or "manualCorrectionAllowed" or "graceTime" or "maxLateAllowedPerMonth" or "penaltyType" or "penaltyCountThreshold" or "earlyExitEnabled" or "earlyExitGraceTime" or "overtimeEnabled" or "overtimeRate" or "minOvertimeMinutes" or "holidayWorkBehavior" or "sandwichRuleEnabled" or "payrollPreviewEnabled" => "Attendance rules",
		"weeklyOffPattern" or "customWeeklyOffs" or "holidaySyncMode" or "holidays" or "holidaySummary" => "Holiday rules",
		"workCalendarPolicyId" => "Assignment/impact references",
		_ => "Other changes"
	};

	private static string ToDisplayText(string value)
	{
		if (string.IsNullOrWhiteSpace(value) || value.Equals("null", StringComparison.OrdinalIgnoreCase))
		{
			return "Not set";
		}

		var spaced = value.Replace("_", " ").Replace("-", " ");
		return string.Join(" ", spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries)
			.Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
	}

	private static string DayName(int day) => day switch
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

	private static string BuildVersionSummary(HrPolicyVersion entity) =>
		$"{ToDisplayText(entity.ChangeAction)} v{entity.VersionNumber}";

	private static T Deserialize<T>(string json) =>
		JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new BusinessException("ShikshaMitra:InvalidPolicyVersionSnapshot");

	private static string BuildRestoredName(string? requestedName, string sourceName, int version) =>
		string.IsNullOrWhiteSpace(requestedName) ? $"{sourceName} v{version} Restore Draft" : requestedName.Trim();

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

	private sealed class VersionDisplayContext
	{
		public Dictionary<Guid, string> PolicyNames { get; } = new();
		public Dictionary<Guid, string> UserNames { get; } = new();
	}
}
