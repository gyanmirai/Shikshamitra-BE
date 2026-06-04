using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Payroll.Services;

public sealed record PayrollCalendarResolution(
	Guid? AcademicYearId,
	IReadOnlyList<Holiday> Holidays,
	decimal WorkingDays);

public class PayrollCalendarResolver : DomainService
{
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly IRepository<Holiday, Guid> _holidayRepository;

	public PayrollCalendarResolver(
		IAcademicYearRepository academicYearRepository,
		IRepository<Holiday, Guid> holidayRepository)
	{
		_academicYearRepository = academicYearRepository;
		_holidayRepository = holidayRepository;
	}

	public Task<PayrollCalendarResolution> ResolveAsync(
		int year,
		int month,
		CancellationToken cancellationToken) =>
		ResolveAsync(year, month, academicYearId: null, cancellationToken);

	public async Task<PayrollCalendarResolution> ResolveAsync(
		int year,
		int month,
		Guid? academicYearId = null,
		CancellationToken cancellationToken = default)
	{
		var firstDay = new DateTime(year, month, 1);
		var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
		var resolvedAcademicYearId = academicYearId
			?? await ResolveAcademicYearIdForMonthAsync(firstDay, lastDay, cancellationToken);
		var holidays = await LoadHolidaysForMonthAsync(firstDay, lastDay, resolvedAcademicYearId, cancellationToken);
		var workingDays = PayrollMonthCalendar.CountWorkingWeekdaysExcludingHolidayWeekdays(year, month, holidays);

		return new PayrollCalendarResolution(resolvedAcademicYearId, holidays, workingDays);
	}

	private async Task<Guid?> ResolveAcademicYearIdForMonthAsync(
		DateTime firstDay,
		DateTime lastDay,
		CancellationToken cancellationToken)
	{
		var academicYearQuery = await _academicYearRepository.GetQueryableAsync();

		return (await AsyncExecuter.FirstOrDefaultAsync(
			academicYearQuery
				.Where(x => x.StartDate <= lastDay && x.EndDate >= firstDay)
				.OrderByDescending(x => x.StartDate),
			cancellationToken))
			?.Id;
	}

	private async Task<List<Holiday>> LoadHolidaysForMonthAsync(
		DateTime firstDay,
		DateTime lastDay,
		Guid? academicYearId,
		CancellationToken cancellationToken)
	{
		var startDate = new DateTimeOffset(firstDay);
		var endDate = new DateTimeOffset(lastDay.AddDays(1).AddTicks(-1));
		var holidayQuery = await _holidayRepository.GetQueryableAsync();

		if (academicYearId.HasValue)
		{
			holidayQuery = holidayQuery.Where(h => h.AcademicYearId == academicYearId.Value);
		}

		var scopedHolidays = await AsyncExecuter.ToListAsync(holidayQuery, cancellationToken);
		return scopedHolidays
			.Where(h => h.StartDate <= endDate && h.EndDate >= startDate)
			.ToList();
	}
}
