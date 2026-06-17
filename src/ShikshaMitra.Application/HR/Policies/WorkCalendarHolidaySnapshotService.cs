using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
public class WorkCalendarHolidaySnapshotService(
	IRepository<WorkCalendarHoliday, Guid> holidaySnapshotRepository,
	IRepository<Holiday, Guid> centralHolidayRepository,
	IRepository<AcademicYear, Guid> academicYearRepository)
	: ApplicationService, ITransientDependency
{
	public async Task<List<WorkCalendarHolidayDto>> GetHolidaysAsync(Guid policyId)
	{
		var query = await holidaySnapshotRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(query
			.Where(x => x.WorkCalendarPolicyId == policyId)
			.OrderBy(x => x.FromDate)
			.ThenBy(x => x.HolidayName));
		return rows.Select(HrPolicyMapping.ToDto).ToList();
	}

	public async Task<Dictionary<Guid, WorkCalendarHolidaySummaryDto>> GetHolidaySummariesAsync(List<Guid> policyIds)
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

	public async Task<List<WorkCalendarHolidayDto>> GetImportPreviewAsync(Guid academicYearId)
	{
		if (academicYearId == Guid.Empty)
		{
			throw new BusinessException(HRErrorCodes.AcademicYearRequired)
				.WithData("field", nameof(academicYearId));
		}

		var query = await centralHolidayRepository.GetQueryableAsync();
		var holidays = await AsyncExecuter.ToListAsync(query
			.Where(x => x.AcademicYearId == academicYearId));

		var rows = new List<WorkCalendarHolidayDto>();
		foreach (var holiday in holidays.OrderBy(x => x.StartDate).ThenBy(x => x.HolidayName))
		{
			var fromDate = holiday.StartDate;
			var toDateOnly = holiday.EndDate < holiday.StartDate ? holiday.StartDate : holiday.EndDate;
			rows.Add(new WorkCalendarHolidayDto
			{
				AcademicYearId = academicYearId,
				Date = fromDate,
				FromDate = fromDate,
				ToDate = toDateOnly,
				DayCount = toDateOnly.DayNumber - fromDate.DayNumber + 1,
				HolidayName = holiday.HolidayName,
				Source = "central_calendar",
				CalendarHolidayId = holiday.Id,
				IsIncluded = true,
				Notes = holiday.Description
			});
		}

		return rows;
	}

	public async Task ValidateHolidaysAsync(Guid academicYearId, List<WorkCalendarHolidayDto>? holidays)
	{
		_ = await NormalizeAndValidateAsync(academicYearId, holidays);
	}

	public async Task ReplaceHolidaysAsync(Guid workCalendarPolicyId, Guid academicYearId, List<WorkCalendarHolidayDto>? holidays)
	{
		var normalizedRows = await NormalizeAndValidateAsync(academicYearId, holidays);

		await holidaySnapshotRepository.DeleteAsync(x => x.WorkCalendarPolicyId == workCalendarPolicyId, autoSave: true);
		foreach (var row in normalizedRows)
		{
			var fromDate = WorkCalendarPolicyChangeDetector.ResolveHolidayFromDate(row);
			var toDate = WorkCalendarPolicyChangeDetector.ResolveHolidayToDate(row);
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

		if (UnitOfWorkManager.Current is not null)
		{
			await UnitOfWorkManager.Current.SaveChangesAsync();
		}
	}

	private async Task<List<WorkCalendarHolidayDto>> NormalizeAndValidateAsync(Guid academicYearId, List<WorkCalendarHolidayDto>? holidays)
	{
		if (academicYearId == Guid.Empty)
		{
			throw new BusinessException(HRErrorCodes.AcademicYearRequired)
				.WithData("field", nameof(academicYearId));
		}

		if (holidays is null || holidays.Count == 0)
		{
			return [];
		}

		var academicYear = await academicYearRepository.GetAsync(academicYearId);
		var normalizedRows = holidays
			.OrderBy(WorkCalendarPolicyChangeDetector.ResolveHolidayFromDate)
			.ThenBy(x => x.HolidayName)
			.ToList();
		WorkCalendarHolidaySnapshotValidator.ValidateRows(normalizedRows, academicYear);
		return normalizedRows;
	}
}
