using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Attendance;

public sealed record AttendanceDailyRegisterState(Guid StandardId, Guid DivisionId, int ExpectedStudents, string Status, bool Provisional);

/// <summary>One dated snapshot for many registers. Uses the daily roster's calendar, credits and revision evaluators.</summary>
[RemoteService(false)]
public class AttendanceDailyRegisterProjection(IRepository<StudentEnrollment, Guid> enrollments, IStudentRepository students,
	IAttendanceDayRepository days, IAttendanceEntryRepository entries, IStudentLeaveRepository leaves,
	IRepository<Division, Guid> divisions, StudentScopeAccessService scope, StudentDirectoryQueryService directory,
	AttendanceApplicabilityResolver applicability, AttendancePolicyResolver policies, IRepository<AcademicYear, Guid> years) : ApplicationService
{
	public async Task<List<AttendanceDailyRegisterState>> ReadAsync(Guid yearId, IReadOnlyCollection<(Guid StandardId, Guid DivisionId)> paths, DateTime date)
	{
		date = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
		await scope.EnsureCanAccessAcademicYearAsync(yearId, "read daily register summary");
		var year = await years.GetAsync(y => y.Id == yearId && y.TenantId == CurrentTenant.Id);
		if (date < year.StartDate.Date || date > year.EndDate.Date) throw new BusinessException(AttendanceOperationErrorCodes.DateOutsideAcademicYear);
		var ids = paths.Select(p => p.DivisionId).Distinct().ToArray();
		if (ids.Length == 0) return [];
		var allowed = await scope.ApplyCurrentUserDivisionScopeAsync((await divisions.GetQueryableAsync()).Where(d => Enumerable.Contains(ids, d.Id)), yearId);
		var allowedPaths = await AsyncExecuter.ToListAsync(allowed.Select(d => new { d.StandardId, d.Id }));
		if (paths.Any(p => !allowedPaths.Any(a => a.Id == p.DivisionId && a.StandardId == p.StandardId)))
			throw new BusinessException(ShikshaMitra.Security.UserScope.UserScopeErrorCodes.AccessDenied);
		var contexts = await applicability.LoadManyAsync(yearId, paths, date, date);
		var headers = await AsyncExecuter.ToListAsync((await days.GetQueryableAsync()).Where(d => d.TenantId == CurrentTenant.Id &&
			d.AcademicYearId == yearId && d.Date == date && Enumerable.Contains(ids, d.DivisionId)));
		var headerIds = headers.Select(d => d.Id).ToArray();
		var observations = await AsyncExecuter.ToListAsync((await entries.GetQueryableAsync()).Where(e => e.TenantId == CurrentTenant.Id &&
			(Enumerable.Contains(headerIds, e.AttendanceDayId) || e.SchoolDate == date)));
		var historicalIds = observations.Where(e => Enumerable.Contains(headerIds, e.AttendanceDayId) && e.EnrollmentId.HasValue).Select(e => e.EnrollmentId!.Value).ToArray();
		// Include historical placements before evaluating the date; current directory membership alone is insufficient.
		var candidates = await AsyncExecuter.ToListAsync((await enrollments.GetQueryableAsync()).Where(e => e.TenantId == CurrentTenant.Id && e.AcademicYearId == yearId &&
			(e.DivisionId.HasValue && Enumerable.Contains(ids, e.DivisionId.Value) || allowed.Any(d => e.AttendancePlacementHistoryJson.Contains(d.Id.ToString().ToLower())) || Enumerable.Contains(historicalIds, e.Id))));
		var studentIds = candidates.Select(e => e.StudentId).Distinct().ToArray();
		var identityIds = (await AsyncExecuter.ToListAsync((await students.GetQueryableWithoutAutoIncludesAsync()).Where(s => s.TenantId == CurrentTenant.Id &&
			Enumerable.Contains(studentIds, s.Id)).Select(s => s.Id))).ToHashSet();
		var activeIds = (await AsyncExecuter.ToListAsync((await directory.GetPopulationAsync(new StudentDirectoryRequest {
			AcademicYearId = yearId, Population = "Active" })).Where(s => Enumerable.Contains(studentIds, s.Id)).Select(s => s.Id))).ToHashSet();
		var policyByDivision = new Dictionary<Guid, AttendancePolicyOptions>();
		var windows = new Dictionary<Guid, (DateTimeOffset Start, DateTimeOffset End)>();
		foreach (var path in paths)
		{
			var header = headers.SingleOrDefault(d => d.DivisionId == path.DivisionId && d.StandardId == path.StandardId);
			var policy = await policies.ResolveAsync(yearId, date, contexts[path.DivisionId].Scope);
			var options = header?.PolicySnapshotJson != null ? JsonSerializer.Deserialize<AttendancePolicyOptions>(header.PolicySnapshotJson)! : policy.Options;
			policyByDivision[path.DivisionId] = options;
			var zone = TimeZoneInfo.FindSystemTimeZoneById(options.SchoolTimeZone);
			windows[path.DivisionId] = (new(TimeZoneInfo.ConvertTimeToUtc(date, zone)), new(TimeZoneInfo.ConvertTimeToUtc(date.AddDays(1), zone)));
		}
		var approved = await leaves.GetApprovedForAttendanceAsync(studentIds, windows.Values.Min(w => w.Start), windows.Values.Max(w => w.End), CurrentTenant.Id);
		var pathSet = paths.ToHashSet(); var memberIds = paths.ToDictionary(p => p.DivisionId, _ => new HashSet<Guid>());
		var candidateById = candidates.ToDictionary(e => e.Id);
		foreach (var enrollment in candidates)
		{
			var placement = enrollment.AttendancePlacementOn(date);
			var standard = placement?.StandardId ?? (!enrollment.AttendanceStartDate.HasValue ? enrollment.StandardId : null);
			var division = placement?.DivisionId ?? (!enrollment.AttendanceStartDate.HasValue ? enrollment.DivisionId : null);
			if (standard.HasValue && division.HasValue && pathSet.Contains((standard.Value, division.Value)) &&
				AttendanceRosterService.IsRosterCandidate(enrollment, standard.Value, division.Value, date, false)) memberIds[division.Value].Add(enrollment.Id);
		}
		foreach (var header in headers)
			foreach (var entry in observations.Where(e => e.AttendanceDayId == header.Id && e.EnrollmentId.HasValue &&
				(AttendanceRosterService.HasRecordedAttendance(e) || header.IsSubmitted && e.ExpectedAtSubmission == true)))
				if (candidateById.ContainsKey(entry.EnrollmentId!.Value)) memberIds[header.DivisionId].Add(entry.EnrollmentId.Value);
		var directoryPaths = candidates.Where(e => activeIds.Contains(e.StudentId)).Select(e => (e.StudentId, e.StandardId, e.DivisionId)).ToHashSet();
		var leavesByStudent = approved.ToLookup(l => l.StudentId); var observationsByEnrollment = observations.ToLookup(e => e.EnrollmentId);
		var result = new List<AttendanceDailyRegisterState>();
		foreach (var path in paths)
		{
			var day = headers.SingleOrDefault(d => d.DivisionId == path.DivisionId && d.StandardId == path.StandardId);
			var recorded = observations.Where(e => e.AttendanceDayId == day?.Id).ToList();
			var options = policyByDivision[path.DivisionId]; var schoolToday = AttendanceSchoolDate.Today(options, Clock.Now);
			var calendar = contexts[path.DivisionId]; var rows = new List<AttendanceRosterRevisionRow>(); var review = false; var visibleExpected = 0;
			foreach (var enrollment in memberIds[path.DivisionId].Select(id => candidateById[id]))
			{
				var submittedMember = day?.IsSubmitted == true && recorded.Any(e => e.EnrollmentId == enrollment.Id && e.ExpectedAtSubmission == true);
				var savedMember = recorded.Any(e => e.EnrollmentId == enrollment.Id && AttendanceRosterService.HasRecordedAttendance(e));
				if (!AttendanceRosterService.IsRosterCandidate(enrollment, path.StandardId, path.DivisionId, date, submittedMember || savedMember) || !identityIds.Contains(enrollment.StudentId)) continue;
				var visible = directoryPaths.Contains((enrollment.StudentId, (Guid?)path.StandardId, (Guid?)path.DivisionId));
				if (!visible && !savedMember) continue;
				var matches = leavesByStudent[enrollment.StudentId].Where(l => l.LeaveDate < windows[path.DivisionId].End &&
					(l.EndDate ?? l.LeaveDate) >= windows[path.DivisionId].Start).ToList();
				if (matches.Count > 1) throw new BusinessException(AttendanceOperationErrorCodes.OverlappingApprovedLeave);
				var entry = recorded.SingleOrDefault(e => e.StudentId == enrollment.StudentId);
				var expected = AttendanceRosterService.IsExpected(enrollment, path.StandardId, path.DivisionId, date, schoolToday,
					submittedMember || !visible && savedMember,
					observationsByEnrollment[enrollment.Id].Any(e => e.SchoolDate == date && e.AttendanceDayId != day?.Id), calendar.For(enrollment.Id, date, schoolToday).IsExpected);
				rows.Add(new(enrollment, expected, calendar.For(enrollment.Id, date, schoolToday).ScheduleLabel, matches.SingleOrDefault()));
				if (visible && expected) visibleExpected++;
				if (day?.IsSubmitted == true && entry != null) review |= AttendanceEntryRules.NeedsCreditCorrection(entry, matches.SingleOrDefault(), options);
			}
			var revision = AttendanceRosterService.RevisionFor(yearId, path.StandardId, path.DivisionId, date, options, rows);
			if (day?.IsSubmitted == true) review |= day.SubmittedRosterRevision != revision;
			result.Add(new(path.StandardId, path.DivisionId, visibleExpected, day?.IsSubmitted == true ? review ? "review" : "submitted" : day == null ? "missing" : "draft",
				day?.IsSubmitted != true && calendar.For(Guid.Empty, date, schoolToday).IsProvisional));
		}
		return result;
	}
}
