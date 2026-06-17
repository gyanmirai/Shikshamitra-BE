using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Calendar.Holidays;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

public class PolicyHolidayImpactFinalizedHolidayReader(
	IRepository<WorkCalendarHoliday, Guid> workCalendarHolidayRepository,
	IRepository<Holiday, Guid> centralHolidayRepository)
	: ApplicationService, ITransientDependency
{
	public async Task<List<WorkCalendarHoliday>> GetAsync(Guid academicYearId, WorkCalendarPolicy workCalendarPolicy)
	{
		var workCalendarHolidayQuery = await workCalendarHolidayRepository.GetQueryableAsync();
		var finalizedHolidays = await AsyncExecuter.ToListAsync(workCalendarHolidayQuery
			.Where(x =>
				x.AcademicYearId == academicYearId &&
				x.WorkCalendarPolicyId == workCalendarPolicy.Id));

		if (workCalendarPolicy.HolidaySyncMode != WorkCalendarHolidaySyncMode.LinkedWithOverrides)
		{
			return finalizedHolidays;
		}

		var existingCalendarHolidayIds = finalizedHolidays
			.Where(x => x.CalendarHolidayId.HasValue)
			.Select(x => x.CalendarHolidayId!.Value)
			.ToHashSet();
		var centralHolidayQuery = await centralHolidayRepository.GetQueryableAsync();
		var centralHolidays = await AsyncExecuter.ToListAsync(centralHolidayQuery
			.Where(x => x.AcademicYearId == academicYearId && !existingCalendarHolidayIds.Contains(x.Id)));
		finalizedHolidays.AddRange(centralHolidays.Select(x => new WorkCalendarHoliday(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			workCalendarPolicy.Id,
			x.StartDate,
			x.EndDate,
			x.HolidayName,
			WorkCalendarHolidaySource.CentralCalendar,
			x.Id,
			isIncluded: true,
			x.Description)));

		return finalizedHolidays;
	}
}
