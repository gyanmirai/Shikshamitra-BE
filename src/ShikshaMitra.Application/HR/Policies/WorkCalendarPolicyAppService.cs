using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
[Authorize]
public class WorkCalendarPolicyAppService(
	IRepository<WorkCalendarPolicy, Guid> repository,
	IRepository<WorkCalendarHoliday, Guid> holidaySnapshotRepository,
	IRepository<Holiday, Guid> centralHolidayRepository,
	IRepository<AcademicYear, Guid> academicYearRepository,
	IHrPolicySafetyAppService policySafetyAppService,
	HrPolicyImpactConfirmationService confirmationService,
	HrPolicyVersionAppService versionAppService,
	HrAuditWriter auditWriter)
	: ApplicationService, IWorkCalendarPolicyAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<PagedResultDto<WorkCalendarPolicyDto>> GetListAsync(PolicyListRequestDto input)
	{
		var query = await repository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == input.AcademicYearId);
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLower();
			query = query.Where(x => x.Name.ToLower().Contains(search));
		}

		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query
			.OrderBy(x => x.Name)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount <= 0 ? 50 : input.MaxResultCount));

		var dtos = rows.Select(HrPolicyMapping.ToDto).ToList();
		var holidaySummaries = await GetHolidaySummariesAsync(rows.Select(x => x.Id).ToList());
		foreach (var dto in dtos)
		{
			dto.HolidaySummary = holidaySummaries.GetValueOrDefault(dto.Id) ?? new WorkCalendarHolidaySummaryDto();
		}

		return new PagedResultDto<WorkCalendarPolicyDto>(total, dtos);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<WorkCalendarPolicyDto> GetAsync(Guid id)
	{
		var dto = HrPolicyMapping.ToDto(await repository.GetAsync(id));
		dto.Holidays = await GetHolidaysAsync(id);
		dto.HolidaySummary = HrPolicyMapping.BuildHolidaySummary(dto.Holidays);
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<List<WorkCalendarHolidayDto>> GetHolidaysAsync(Guid id)
	{
		var query = await holidaySnapshotRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(query
			.Where(x => x.WorkCalendarPolicyId == id)
			.OrderBy(x => x.FromDate)
			.ThenBy(x => x.HolidayName));
		return rows.Select(HrPolicyMapping.ToDto).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.View)]
	public async Task<List<WorkCalendarHolidayDto>> GetHolidayImportPreviewAsync(Guid academicYearId)
	{
		if (academicYearId == Guid.Empty)
		{
			throw new BusinessException("ShikshaMitra:AcademicYearRequired")
				.WithData("field", nameof(academicYearId));
		}

		var query = await centralHolidayRepository.GetQueryableAsync();
		var holidays = await AsyncExecuter.ToListAsync(query
			.Where(x => x.AcademicYearId == academicYearId));

		var rows = new List<WorkCalendarHolidayDto>();
		foreach (var holiday in holidays.OrderBy(x => x.StartDate).ThenBy(x => x.HolidayName))
		{
			var fromDate = holiday.StartDate.Date;
			var toDate = holiday.EndDate.Date < fromDate ? fromDate : holiday.EndDate.Date;
			rows.Add(new WorkCalendarHolidayDto
			{
				AcademicYearId = academicYearId,
				Date = fromDate,
				FromDate = fromDate,
				ToDate = toDate,
				DayCount = (toDate - fromDate).Days + 1,
				HolidayName = holiday.HolidayName,
				Source = "central_calendar",
				CalendarHolidayId = holiday.Id,
				IsIncluded = true,
				Notes = holiday.Description
			});
		}

		return rows;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Create)]
	public async Task<WorkCalendarPolicyDto> CreateAsync(CreateUpdateWorkCalendarPolicyDto input)
	{
		var entity = new WorkCalendarPolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			input.AcademicYearId,
			input.Name,
			HrPolicyMapping.ParsePolicyStatus(input.Status),
			input.EffectiveFrom,
			input.EffectiveTo,
			HrPolicyMapping.ParseWeeklyOffPattern(input.WeeklyOffPattern),
			input.CustomWeeklyOffs,
			HrPolicyMapping.ParseHolidaySyncMode(input.HolidaySyncMode));

		await repository.InsertAsync(entity, autoSave: true);
		await ReplaceHolidaysAsync(entity.Id, input.AcademicYearId, input.Holidays);
		var dto = HrPolicyMapping.ToDto(entity);
		dto.Holidays = await GetHolidaysAsync(entity.Id);
		dto.HolidaySummary = HrPolicyMapping.BuildHolidaySummary(dto.Holidays);
		await versionAppService.CaptureAsync("work_calendar", dto, "created");
		await auditWriter.WriteAsync(dto.AcademicYearId, "work_calendar_policy", dto.Id, dto.Name, "created", $"Created work calendar policy {dto.Name}");
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Edit)]
	public async Task<WorkCalendarPolicyDto> UpdateAsync(Guid id, CreateUpdateWorkCalendarPolicyDto input)
	{
		var entity = await repository.GetAsync(id);
		var before = HrPolicyMapping.ToDto(entity);
		if (entity.Status != HrPolicyStatus.Draft || HrPolicyMapping.ParsePolicyStatus(input.Status) != HrPolicyStatus.Draft)
		{
			var preview = await policySafetyAppService.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
			{
				PolicyType = "work_calendar",
				PolicyId = id,
				AcademicYearId = entity.AcademicYearId,
				Action = "update",
				WorkCalendarPolicy = input
			});
			await confirmationService.EnsureConfirmedAsync(preview);
		}
		entity.Update(
			input.Name,
			HrPolicyMapping.ParsePolicyStatus(input.Status),
			input.EffectiveFrom,
			input.EffectiveTo,
			HrPolicyMapping.ParseWeeklyOffPattern(input.WeeklyOffPattern),
			input.CustomWeeklyOffs,
			HrPolicyMapping.ParseHolidaySyncMode(input.HolidaySyncMode));

		await repository.UpdateAsync(entity, autoSave: true);
		await ReplaceHolidaysAsync(entity.Id, input.AcademicYearId, input.Holidays);
		var dto = HrPolicyMapping.ToDto(entity);
		dto.Holidays = await GetHolidaysAsync(entity.Id);
		dto.HolidaySummary = HrPolicyMapping.BuildHolidaySummary(dto.Holidays);
		await versionAppService.CaptureAsync("work_calendar", dto, "edited");
		await auditWriter.WriteAsync(dto.AcademicYearId, "work_calendar_policy", dto.Id, dto.Name, "edited", $"Edited work calendar policy {dto.Name}", changedFields: HrAuditWriter.Diff(before, dto, "lastModificationTime", "lastModifierId"));
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Clone)]
	public async Task<WorkCalendarPolicyDto> CloneAsync(Guid id, HrPolicyCloneRequestDto input)
	{
		var source = await repository.GetAsync(id);
		var entity = new WorkCalendarPolicy(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			source.AcademicYearId,
			string.IsNullOrWhiteSpace(input.Name) ? $"{source.Name} Copy" : input.Name!,
			HrPolicyStatus.Draft,
			input.EffectiveFrom?.Date ?? source.EffectiveFrom,
			source.EffectiveTo,
			source.WeeklyOffPattern,
			source.GetCustomWeeklyOffs(),
			source.HolidaySyncMode);

		await repository.InsertAsync(entity, autoSave: true);
		var sourceHolidays = await GetHolidaysAsync(source.Id);
		await ReplaceHolidaysAsync(entity.Id, source.AcademicYearId, sourceHolidays);
		var dto = HrPolicyMapping.ToDto(entity);
		dto.Holidays = await GetHolidaysAsync(entity.Id);
		dto.HolidaySummary = HrPolicyMapping.BuildHolidaySummary(dto.Holidays);
		await versionAppService.CaptureAsync("work_calendar", dto, "cloned");
		await auditWriter.WriteAsync(dto.AcademicYearId, "work_calendar_policy", dto.Id, dto.Name, "cloned", $"Cloned work calendar policy from {source.Name}", context: new { sourcePolicyId = source.Id });
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Policies.Archive)]
	public async Task DeleteAsync(Guid id)
	{
		var entity = await repository.GetAsync(id);
		var preview = await policySafetyAppService.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "work_calendar",
			PolicyId = id,
			AcademicYearId = entity.AcademicYearId,
			Action = "archive"
		});
		await confirmationService.EnsureConfirmedAsync(preview);
		entity.Archive();
		await repository.UpdateAsync(entity, autoSave: true);
		var dto = HrPolicyMapping.ToDto(entity);
		await versionAppService.CaptureAsync("work_calendar", dto, "archived");
		await auditWriter.WriteAsync(dto.AcademicYearId, "work_calendar_policy", dto.Id, dto.Name, "archived", $"Archived work calendar policy {dto.Name}");
	}

	private async Task<Dictionary<Guid, WorkCalendarHolidaySummaryDto>> GetHolidaySummariesAsync(List<Guid> policyIds)
	{
		if (policyIds.Count == 0)
		{
			return new Dictionary<Guid, WorkCalendarHolidaySummaryDto>();
		}

		var query = await holidaySnapshotRepository.GetQueryableAsync();
		var holidays = await AsyncExecuter.ToListAsync(query.Where(x => policyIds.Contains(x.WorkCalendarPolicyId)));
		return holidays
			.Select(HrPolicyMapping.ToDto)
			.GroupBy(x => x.WorkCalendarPolicyId!.Value)
			.ToDictionary(x => x.Key, x => HrPolicyMapping.BuildHolidaySummary(x));
	}

	private async Task ReplaceHolidaysAsync(Guid workCalendarPolicyId, Guid academicYearId, List<WorkCalendarHolidayDto>? holidays)
	{
		if (academicYearId == Guid.Empty)
		{
			throw new BusinessException("ShikshaMitra:AcademicYearRequired")
				.WithData("field", nameof(academicYearId));
		}

		await holidaySnapshotRepository.DeleteAsync(x => x.WorkCalendarPolicyId == workCalendarPolicyId, autoSave: true);
		if (holidays is null || holidays.Count == 0)
		{
			return;
		}

		var academicYear = await academicYearRepository.GetAsync(academicYearId);
		foreach (var row in holidays
			.OrderBy(x => ResolveHolidayFromDate(x))
			.ThenBy(x => x.HolidayName))
		{
			ValidateHolidayRow(row, academicYear);
			var fromDate = ResolveHolidayFromDate(row);
			var toDate = ResolveHolidayToDate(row);
			var entity = new WorkCalendarHoliday(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				academicYearId,
				workCalendarPolicyId,
				fromDate,
				toDate,
				row.HolidayName,
				HrPolicyMapping.ParseWorkCalendarHolidaySource(row.Source),
				row.CalendarHolidayId,
				row.IsIncluded,
				row.Notes);
			await holidaySnapshotRepository.InsertAsync(entity, autoSave: false);
		}
	}

	private static void ValidateHolidayRow(WorkCalendarHolidayDto row, AcademicYear academicYear)
	{
		var fromDate = ResolveHolidayFromDate(row);
		var toDate = ResolveHolidayToDate(row);
		if (fromDate == default)
		{
			throw new BusinessException("ShikshaMitra:WorkCalendarHolidayDateRequired")
				.WithData("field", nameof(row.FromDate));
		}

		if (toDate == default)
		{
			throw new BusinessException("ShikshaMitra:WorkCalendarHolidayDateRequired")
				.WithData("field", nameof(row.ToDate));
		}

		if (toDate.Date < fromDate.Date)
		{
			throw new BusinessException("ShikshaMitra:WorkCalendarHolidayInvalidRange")
				.WithData("field", nameof(row.ToDate))
				.WithData("fromDate", fromDate.ToString("yyyy-MM-dd"))
				.WithData("toDate", toDate.ToString("yyyy-MM-dd"));
		}

		if (string.IsNullOrWhiteSpace(row.HolidayName))
		{
			throw new BusinessException("ShikshaMitra:WorkCalendarHolidayNameRequired")
				.WithData("field", nameof(row.HolidayName))
				.WithData("date", fromDate.ToString("yyyy-MM-dd"));
		}

		if (row.AcademicYearId != Guid.Empty && row.AcademicYearId != academicYear.Id)
		{
			throw new BusinessException("ShikshaMitra:WorkCalendarHolidayAcademicYearMismatch")
				.WithData("field", nameof(row.AcademicYearId))
				.WithData("date", fromDate.ToString("yyyy-MM-dd"));
		}

		if (fromDate.Date < academicYear.StartDate.Date || toDate.Date > academicYear.EndDate.Date)
		{
			throw new BusinessException("ShikshaMitra:WorkCalendarHolidayOutsideAcademicYear")
				.WithData("field", nameof(row.FromDate))
				.WithData("fromDate", fromDate.ToString("yyyy-MM-dd"))
				.WithData("toDate", toDate.ToString("yyyy-MM-dd"))
				.WithData("academicYear", academicYear.Name);
		}
	}

	private static DateTime ResolveHolidayFromDate(WorkCalendarHolidayDto row) =>
		(row.FromDate ?? row.Date).Date;

	private static DateTime ResolveHolidayToDate(WorkCalendarHolidayDto row) =>
		(row.ToDate ?? row.FromDate ?? row.Date).Date;
}
