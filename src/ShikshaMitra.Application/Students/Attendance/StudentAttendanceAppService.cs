using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;

namespace ShikshaMitra.Students.Attendance;

[Authorize]
[RemoteService(false)]
public class StudentAttendanceAppService(AttendanceRosterService rosters, AttendanceDailyCommandService commands,
	AttendanceManager manager, IAttendanceDayRepository days, IAttendanceEntryRepository entries,
	StudentScopeAccessService scope, IPermissionChecker permissions, AttendanceReportService reports, AttendanceRegisterQueryService register) : ApplicationService, IStudentAttendanceAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View)]
	public async Task<StudentAttendanceListDto> GetStudentsForDateAsync(GetStudentsForAttendanceInput input)
	{
		var roster = await rosters.LoadAsync(input.AcademicYearId, input.StandardId, input.DivisionId, input.Date);
		var range = await reports.RangeAsync(input.AcademicYearId, default, input.Date);
		var cumulative = (await register.BuildRowsAsync(input.AcademicYearId, range.Start, range.End,
			roster.Members.Select(r => r.Enrollment).ToList(), includeDaily: false)).ToDictionary(r => r.StudentId);
		var students = roster.Members.Select(r => new StudentAttendanceDto {
			StudentId = r.Student.Id, EnrollmentId = r.Enrollment.Id, GrNo = r.Student.GrNo, RollNo = r.RollNumber,
			FirstName = r.Student.FirstName, MiddleName = r.Student.MiddleName, LastName = r.Student.LastName,
			Status = r.Entry?.Status ?? AttendanceStatus.Pending, Remarks = r.Entry?.Remarks, Late = r.Entry?.Late ?? false,
			HalfDay = r.Entry?.HalfDay ?? false, IsExpected = r.Expected, NotExpectedReason = r.Reason,
			NeedsCreditCorrection = AttendanceEntryRules.NeedsCreditCorrection(r, roster),
			RequiresCorrectionApproval = r.Entry?.Status is AttendanceStatus.Present or AttendanceStatus.Absent &&
				(roster.Day?.IsLocked == true && roster.Options.LockedCorrectionRequiresApproval || roster.Date < AttendanceSchoolDate.Today(roster.Options, Clock.Now) && roster.Options.PriorDayCorrectionRequiresApproval),
			PresentDays = cumulative.GetValueOrDefault(r.Student.Id)?.PresentDays ?? 0, AttendancePercentage = cumulative.GetValueOrDefault(r.Student.Id)?.Percentage,
			ExpectedDays = cumulative.GetValueOrDefault(r.Student.Id)?.ExpectedDays ?? 0, RecordedDays = cumulative.GetValueOrDefault(r.Student.Id)?.RecordedDays ?? 0,
			MissingDays = cumulative.GetValueOrDefault(r.Student.Id)?.MissingDays ?? 0, IsProvisional = cumulative.GetValueOrDefault(r.Student.Id)?.IsProvisional ?? true,
			ApprovedLeaveId = r.Leave?.Id, ScheduleLabel = r.ScheduleLabel }).ToList();
		return new() { AttendanceDayId = roster.Day?.Id, Date = roster.Date, IsLocked = roster.Day?.IsLocked ?? false,
			IsSubmitted = roster.Day?.IsSubmitted ?? false, Revision = roster.Day?.Revision.ToString() ?? "0",
			NeedsReview = roster.Day?.IsSubmitted == true && (roster.Day.SubmittedRosterRevision != roster.Revision || students.Any(s => s.NeedsCreditCorrection)),
			IsPastMarkingCutoff = roster.Day?.IsSubmitted != true && roster.SchoolDay.IsExpected && AttendanceSchoolDate.PastMarkingCutoff(roster.Options, roster.Date, Clock.Now),
			RosterRevision = roster.Revision, Policy = roster.Options, IsExpected = roster.SchoolDay.IsExpected,
			NotExpectedReason = roster.SchoolDay.IsExpected ? null : roster.SchoolDay.Reason,
			CanMark = roster.Date <= AttendanceSchoolDate.Today(roster.Options, Clock.Now) && roster.SchoolDay.IsExpected && !(roster.Day?.IsLocked ?? false) &&
				await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Add),
			TotalStudents = students.Count(s => s.IsExpected), PresentCount = students.Count(s => s.IsExpected && s.Status == AttendanceStatus.Present),
			AbsentCount = students.Count(s => s.IsExpected && s.Status == AttendanceStatus.Absent), PendingCount = students.Count(s => s.IsExpected && s.Status == AttendanceStatus.Pending), Students = students };
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Add)]
	public Task<MarkAttendanceOutput> MarkAttendanceAsync(MarkAttendanceInput input) => commands.SaveAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Delete)]
	public async Task<LockAttendanceDayOutput> LockAttendanceDayAsync(LockAttendanceDayInput input)
	{
		var roster = await rosters.LoadAsync(input.AcademicYearId, input.StandardId, input.DivisionId, input.Date);
		var day = roster.Day ?? throw new BusinessException("SM:AttendanceDayNotFound");
		if (!day.IsSubmitted || roster.Members.Any(r => r.Expected && (r.Entry == null || r.Entry.Status == AttendanceStatus.Pending)))
			throw new BusinessException(AttendanceOperationErrorCodes.IncompleteRoster);
		await manager.LockAttendanceDayAsync(day.Id);
		return new() { AttendanceDayId = day.Id, IsLocked = true, Message = "Attendance day locked" };
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Delete)]
	public async Task<LockAttendanceDayOutput> UnlockAttendanceDayAsync(LockAttendanceDayInput input)
	{
		var roster = await rosters.LoadAsync(input.AcademicYearId, input.StandardId, input.DivisionId, input.Date);
		var day = roster.Day ?? throw new BusinessException("SM:AttendanceDayNotFound");
		await manager.UnlockAttendanceDayAsync(day.Id);
		return new() { AttendanceDayId = day.Id, IsLocked = false, Message = "Attendance day unlocked" };
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View)]
	public async Task<PagedResultDto<StudentAttendanceHistoryDto>> GetAttendanceHistoryAsync(GetStudentAttendanceHistoryInput input)
	{
		await scope.GetStudentAsync(input.StudentId, nameof(GetAttendanceHistoryAsync));
		var today = Clock.Now.Date;
		var start = (input.StartDate ?? new DateTime(today.Year, today.Month, 1)).Date;
		var end = (input.EndDate ?? new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1)).Date;
		if (start > end) throw new BusinessException(AttendanceOperationErrorCodes.InvalidDateRange);
		var scopedDays = await scope.ApplyCurrentUserAttendanceDayScopeAsync(await days.GetQueryableAsync());
		var query = from entry in await entries.GetQueryableAsync()
			join day in scopedDays on entry.AttendanceDayId equals day.Id
			where entry.StudentId == input.StudentId && day.Date >= start && day.Date <= end
			select new StudentAttendanceHistoryDto { AttendanceDayId = day.Id, Date = day.Date, Status = entry.Status, Remarks = entry.Remarks };
		var count = await AsyncExecuter.CountAsync(query);
		var parts = (input.Sorting ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
			.FirstOrDefault()?.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
		var descending = parts.Length < 2 || parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
		query = parts.FirstOrDefault()?.ToLowerInvariant() switch {
			"status" => descending ? query.OrderByDescending(x => x.Status).ThenByDescending(x => x.Date).ThenBy(x => x.AttendanceDayId) : query.OrderBy(x => x.Status).ThenBy(x => x.Date).ThenBy(x => x.AttendanceDayId),
			"remarks" => descending ? query.OrderByDescending(x => x.Remarks).ThenByDescending(x => x.Date).ThenBy(x => x.AttendanceDayId) : query.OrderBy(x => x.Remarks).ThenBy(x => x.Date).ThenBy(x => x.AttendanceDayId),
			"date" when !descending => query.OrderBy(x => x.Date).ThenBy(x => x.AttendanceDayId),
			_ => query.OrderByDescending(x => x.Date).ThenBy(x => x.AttendanceDayId) };
		return new(count, await AsyncExecuter.ToListAsync(query.Skip(input.SkipCount).Take(input.MaxResultCount)));
	}
}
