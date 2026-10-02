using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.Records;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Attendance;

[RemoteService(false)]
public class AttendanceRegisterQueryService(IRepository<StudentEnrollment, Guid> enrollments,
	StudentRecordsEnrollmentScopeService enrollmentScopes, StudentScopeAccessService scope,
	IAttendanceDayRepository days, IAttendanceEntryRepository entries, AttendanceReportService reports, IRepository<Division, Guid> divisions)
	: ApplicationService, ITransientDependency
{
	public async Task<AttendanceRegisterDto> ReadAsync(AttendanceRegisterInput input)
	{
		var range = await reports.RangeAsync(input.AcademicYearId, input.StartDate, input.EndDate);
		var scopedMembers = await enrollmentScopes.GetQueryableAsync();
		var scopedDays = (await scope.ApplyCurrentUserAttendanceDayScopeAsync(await days.GetQueryableAsync())).Where(d =>
			d.TenantId == CurrentTenant.Id && d.AcademicYearId == input.AcademicYearId &&
			(!input.StandardId.HasValue || d.StandardId == input.StandardId) && (!input.DivisionId.HasValue || d.DivisionId == input.DivisionId));
		var historicIds = from entry in await entries.GetQueryableAsync() join day in scopedDays on entry.AttendanceDayId equals day.Id
			where day.Date >= range.Start && day.Date <= range.End select entry.EnrollmentId;
		var authorizedDivisions = await scope.ApplyCurrentUserDivisionScopeAsync((await divisions.GetQueryableAsync()).Where(d =>
			d.TenantId == CurrentTenant.Id && (!input.StandardId.HasValue || d.StandardId == input.StandardId) &&
			(!input.DivisionId.HasValue || d.Id == input.DivisionId)), input.AcademicYearId);
		var authorizedPaths = (await AsyncExecuter.ToListAsync(authorizedDivisions.Select(d => new { d.StandardId, DivisionId = d.Id })))
			.Select(d => (d.StandardId, d.DivisionId)).ToHashSet();
		var historicalCandidates = await AsyncExecuter.ToListAsync((await enrollments.GetQueryableAsync()).Where(e =>
			e.TenantId == CurrentTenant.Id && e.AcademicYearId == input.AcademicYearId &&
			authorizedDivisions.Any(d => e.AttendancePlacementHistoryJson.Contains(d.Id.ToString().ToLower()))));
		// Filter dated paths before counting/paging; an old division outside this range must not create an empty page.
		var historicalEnrollmentIds = historicalCandidates.Where(e => e.AttendancePlacementsBetween(range.Start, range.End)
			.Any(p => p.StandardId.HasValue && p.DivisionId.HasValue && authorizedPaths.Contains((p.StandardId.Value, p.DivisionId.Value))))
			.Select(e => e.Id).ToArray();
		var query = (await enrollments.GetQueryableAsync()).Where(e => e.TenantId == CurrentTenant.Id && e.AcademicYearId == input.AcademicYearId &&
			(scopedMembers.Any(m => m.Id == e.Id) && (!input.StandardId.HasValue || e.StandardId == input.StandardId) && (!input.DivisionId.HasValue || e.DivisionId == input.DivisionId) ||
			 historicIds.Any(id => id == e.Id) || Enumerable.Contains(historicalEnrollmentIds, e.Id)));
		var count = await AsyncExecuter.CountAsync(query);
		var members = await AsyncExecuter.ToListAsync(query.OrderBy(e => e.RollNumber).ThenBy(e => e.Id).Skip(input.SkipCount).Take(input.MaxResultCount));
		return new() { TotalCount = count, Rows = await BuildRowsAsync(input.AcademicYearId, range.Start, range.End, members,
			input.StandardId, input.DivisionId, includeDaily: false) };
	}

	public async Task<List<AttendanceSummaryDto>> BuildRowsAsync(Guid yearId, DateTime start, DateTime end, IReadOnlyCollection<StudentEnrollment> members,
		Guid? standardId = null, Guid? divisionId = null, bool includeDaily = false)
	{
		var paths = new Dictionary<(Guid Standard, Guid Division), HashSet<Guid>>();
		void Add(Guid? standard, Guid? division, Guid enrollmentId)
		{
			if (!standard.HasValue || !division.HasValue || standardId.HasValue && standard != standardId || divisionId.HasValue && division != divisionId) return;
			var path = (standard.Value, division.Value);
			if (!paths.TryGetValue(path, out var cohort)) paths[path] = cohort = [];
			cohort.Add(enrollmentId);
		}
		foreach (var member in members)
		{
			Add(member.StandardId, member.DivisionId, member.Id);
			foreach (var placement in member.AttendancePlacementsBetween(start, end))
				Add(placement.StandardId, placement.DivisionId, member.Id);
		}
		var memberIds = members.Select(e => (Guid?)e.Id).ToArray();
		var scopedDays = await scope.ApplyCurrentUserAttendanceDayScopeAsync(await days.GetQueryableAsync());
		var historicPaths = await AsyncExecuter.ToListAsync(from entry in await entries.GetQueryableAsync()
			join day in scopedDays on entry.AttendanceDayId equals day.Id
			where day.TenantId == CurrentTenant.Id && day.AcademicYearId == yearId && day.Date >= start && day.Date <= end && Enumerable.Contains(memberIds, entry.EnrollmentId)
			select new { entry.EnrollmentId, day.StandardId, day.DivisionId });
		foreach (var path in historicPaths) Add(path.StandardId, path.DivisionId, path.EnrollmentId!.Value);
		var slices = new List<AttendanceSummaryDto>(); var partial = new HashSet<Guid>();
		foreach (var path in paths)
		{
			try { slices.AddRange(await reports.BuildAsync(yearId, path.Key.Standard, path.Key.Division, start, end, members.Where(e => path.Value.Contains(e.Id)).ToList(), includeDaily: true)); }
			catch (BusinessException exception) when (exception.Code == ShikshaMitra.Security.UserScope.UserScopeErrorCodes.AccessDenied)
			{ foreach (var id in path.Value) partial.Add(id); }
		}
		return slices.GroupBy(s => s.EnrollmentId).Select(group =>
		{
			var row = group.First();
			var daily = group.SelectMany(s => s.Daily).GroupBy(d => d.Date).Select(g => g.Count(d => d.Recorded) > 1
				? new AttendanceSummaryDayDto { Date = g.Key, Status = AttendanceStatus.Pending, Expected = g.Any(d => d.Expected), IsProvisional = true,
					ExclusionReason = "Conflicting historical attendance records require review" }
				: g.FirstOrDefault(d => d.Recorded) ?? g.FirstOrDefault(d => d.Expected) ?? g.First()).OrderBy(d => d.Date).ToList();
			AttendanceReportService.ApplyCalculation(row, daily); row.IsProvisional |= partial.Contains(row.EnrollmentId);
			row.Daily = includeDaily ? daily : []; return row;
		}).ToList();
	}
}
