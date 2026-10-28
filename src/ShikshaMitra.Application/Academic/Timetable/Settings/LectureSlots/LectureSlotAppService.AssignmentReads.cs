using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.Standards;

namespace ShikshaMitra.Academic.Timetable.Settings.LectureSlots;

public partial class LectureSlotAppService
{
	private async Task<AssignmentReadContext> LoadAssignmentReadsAsync(IReadOnlyCollection<Guid> selectedStandards)
	{
		var ids = selectedStandards.Distinct().ToArray();
		var standards = await standardRepository.GetListAsync(standard => Enumerable.Contains(ids, standard.Id));
		var shifts = await LoadClassShiftInfosAsync(standards);
		var query = await timetableRepository.GetQueryableAsync();
		var timingQuery = from timetable in query
			where Enumerable.Contains(ids, timetable.StandardId)
			from slot in timetable.Slots
			where slot.Subjects.Any()
			select new ActiveAssignmentTiming(timetable.StandardId, slot.Day, slot.StartTime, slot.EndTime, slot.SlotType);
		var timings = await AsyncExecuter.ToListAsync(timingQuery.Distinct());
		var configurations = await AsyncExecuter.ToListAsync((await lectureSlotConfigRepository.WithDetailsAsync())
			.Where(configuration => Enumerable.Contains(ids, configuration.StandardId)));
		return new AssignmentReadContext(standards.ToDictionary(standard => standard.Id), shifts,
			timings.ToLookup(timing => timing.StandardId), configurations.ToDictionary(configuration => configuration.StandardId));
	}

	private sealed record AssignmentReadContext(Dictionary<Guid, Standard> Standards, Dictionary<Guid, ClassShiftInfo> Shifts,
		ILookup<Guid, ActiveAssignmentTiming> Timings, Dictionary<Guid, LectureSlotConfig> Configurations);
	private sealed record ActiveAssignmentTiming(Guid StandardId, TimetableDayOfWeek Day, TimeOnly StartTime, TimeOnly EndTime, SlotType SlotType);
}
