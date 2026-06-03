using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Calendar.Holidays;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;

namespace ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;

[RemoteService(false)]
public class TakeAttendanceHolidayReader(
	IRepository<Holiday, Guid> holidayRepository)
	: ApplicationService, ITransientDependency
{
	public async Task<Holiday?> FindHolidayAsync(DateOnly date)
	{
		var dayStart = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
		var dayEnd = new DateTimeOffset(date.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
		var query = await holidayRepository.GetQueryableAsync();

		var holidays = await AsyncExecuter.ToListAsync(query);
		return holidays
			.Where(x => x.StartDate <= dayEnd && x.EndDate >= dayStart)
			.OrderBy(x => x.StartDate)
			.FirstOrDefault();
	}
}
