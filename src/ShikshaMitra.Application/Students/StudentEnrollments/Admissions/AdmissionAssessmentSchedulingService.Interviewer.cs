using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionAssessmentSchedulingService
{
	private async Task EnsureInterviewerAvailableAsync(Employee employee, AdmissionAssessmentSessionInput input)
	{
		var window = SchoolWindow(input.StartsAt.UtcDateTime, input.EndsAt.UtcDateTime);
		var start = TimeOnly.FromTimeSpan(window.Start); var end = TimeOnly.FromTimeSpan(window.End);
		var teacherIds = new[] { employee.Id, employee.UserId.GetValueOrDefault() }.Where(id => id != Guid.Empty).Distinct().ToArray();
		var availability = LazyServiceProvider.LazyGetRequiredService<IRepository<TeacherAvailability, Guid>>();
		if (await availability.AnyAsync(item => item.TeacherId.HasValue && Enumerable.Contains(teacherIds, item.TeacherId.Value) &&
			!item.IsCancelled && item.AvailabilityDate == window.Date && (item.IsFullDay || item.StartTime < end && item.EndTime > start)))
			throw Invalid("The interviewer is marked unavailable at this time.");
		var substitutions = LazyServiceProvider.LazyGetRequiredService<IRepository<SubstitutionRequest, Guid>>();
		if (await substitutions.AnyAsync(item => item.SubstituteTeacherId.HasValue && Enumerable.Contains(teacherIds, item.SubstituteTeacherId.Value) &&
			item.Date == window.Date && item.StartTime < end && item.EndTime > start &&
			(item.Status == SubstitutionStatus.Assigned || item.Status == SubstitutionStatus.WaitingAck || item.Status == SubstitutionStatus.Acknowledged || item.Status == SubstitutionStatus.Completed)))
			throw Invalid("The interviewer has a substitution assignment at this time.");
		if (window.Date.DayOfWeek == DayOfWeek.Sunday) return;
		var day = (TimetableDayOfWeek)((int)window.Date.DayOfWeek - 1);
		var years = await LazyServiceProvider.LazyGetRequiredService<IRepository<AcademicYear, Guid>>().GetQueryableAsync();
		var yearId = await AsyncExecuter.FirstOrDefaultAsync(years.Where(year => year.StartDate.Date <= window.Date && year.EndDate.Date >= window.Date)
			.OrderByDescending(year => year.StartDate).Select(year => (Guid?)year.Id));
		var timetableRepository = LazyServiceProvider.LazyGetRequiredService<ITimetableRepository>();
		// Reuse the existing Employee/User reference compatibility used by Teacher Availability.
		// This is an availability veto, not a projection of another class's private timetable data.
		foreach (var id in teacherIds)
		{
			var timetables = await timetableRepository.GetByEmployeeIdAsync(id, yearId);
			if (timetables.Any(timetable => timetable.Status == TimetableStatus.Published && timetable.Slots.Any(slot => slot.Day == day &&
				slot.StartTime < end && slot.EndTime > start && slot.Subjects.Any(subject => subject.TeacherId.HasValue && teacherIds.Contains(subject.TeacherId.Value)))))
				throw Invalid("The interviewer has a published teaching assignment at this time.");
		}
	}
}
