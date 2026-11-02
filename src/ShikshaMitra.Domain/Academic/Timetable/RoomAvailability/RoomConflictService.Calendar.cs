using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public partial class RoomConflictService
{
	private async Task<Guid?> ResolveCalendarMediumAsync(Guid? standardId, Guid? mediumId)
	{
		var resolved = standardId.HasValue
			? await AsyncExecuter.FirstOrDefaultAsync((await _standardRepository.GetQueryableAsync())
				.Where(row => row.Id == standardId.Value)
				.Select(row => (Guid?)row.EducationMediumStage.EducationMediumId)) ?? mediumId
			: mediumId;
		if (resolved.HasValue) await ValidateCalendarMediumsAsync([resolved.Value]);
		return resolved;
	}

	private async Task ValidateCalendarMediumsAsync(IReadOnlyCollection<Guid> mediumIds)
	{
		if (mediumIds.Count == 0) return;
		var repository = LazyServiceProvider.LazyGetRequiredService<IRepository<EducationMedium, Guid>>();
		var found = await AsyncExecuter.ToListAsync((await repository.GetQueryableAsync())
			.Where(row => Enumerable.Contains(mediumIds, row.Id)).Select(row => row.Id));
		foreach (var mediumId in mediumIds)
			if (!found.Contains(mediumId)) throw new EntityNotFoundException(typeof(EducationMedium), mediumId);
	}

	private static AcademicCalendar? SelectRoomCalendar(IEnumerable<AcademicCalendar> calendars, DateTime date,
		Guid? mediumId = null, RoomConflictAcademicScope? scope = null) =>
		calendars.Where(calendar => calendar.Status != CalendarStatus.Archived &&
			(scope == null || calendar.Status == CalendarStatus.Published) &&
			calendar.StartDate.Date <= date.Date && calendar.EndDate.Date >= date.Date &&
			(!mediumId.HasValue || calendar.AcademicMediumId == mediumId.Value) &&
			(scope == null || calendar.AcademicYearId == scope.AcademicYearId ||
				calendar.AcademicYearId == null && scope.MediumAcademicYearId == scope.AcademicYearId))
			.OrderByDescending(calendar => calendar.Status == CalendarStatus.Published ? 2 : calendar.IsDefault ? 1 : 0)
			.ThenByDescending(calendar => calendar.CreationTime).ThenByDescending(calendar => calendar.Id)
			.FirstOrDefault();

	private async Task<AcademicCalendar?> ReadRoomCalendarAsync(DateTime date, Guid? mediumId,
		RoomConflictAcademicScope? scope = null) =>
		SelectRoomCalendar(await _calendarRepository.GetListAsync(calendar => calendar.Status != CalendarStatus.Archived &&
			calendar.StartDate.Date <= date.Date && calendar.EndDate.Date >= date.Date &&
			(!mediumId.HasValue || calendar.AcademicMediumId == mediumId.Value)), date, mediumId, scope);

	private static bool BlocksRoomDay(AcademicCalendarDay day, Guid? standardId, Guid? divisionId) =>
		CalendarDayBlockingRules.BlocksTimetable(day.DayType) &&
		(day.AppliesToAll || ((day.StandardId.HasValue || day.DivisionId.HasValue) &&
			(!day.StandardId.HasValue || day.StandardId == standardId) &&
			(!day.DivisionId.HasValue || day.DivisionId == divisionId)));
}
