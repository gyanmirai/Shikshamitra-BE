using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Attendance;

[RemoteService(false)]
public class AttendanceWorkspaceQueryService(StudentDirectoryQueryService directory, StudentDirectoryOptionsService options,
	AttendanceWorkspaceAccessService access, AttendanceRosterService rosters, AttendancePolicyResolver policies,
	IPermissionChecker permissions, IRepository<AcademicYear, Guid> years, StudentScopeAccessService scope,
	IAttendanceDayRepository days, IAttendanceEntryRepository entries)
	: ApplicationService, ITransientDependency
{
	public async Task<AttendanceWorkspaceContextDto> ContextAsync(Guid yearId)
	{
		await AttendanceActionAuthorization.EnsureAsync(permissions, ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View);
		var isAdmin = await access.IsAdminAsync();
		var result = new AttendanceWorkspaceContextDto {
			IsAdmin = isAdmin, Options = await options.GetAsync(yearId),
			SchoolToday = AttendanceSchoolDate.Today((await policies.ResolveAsync(yearId, Clock.Now.Date)).Options, Clock.Now),
			CanManageLock = isAdmin && await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Delete),
			AssignedClasses = await access.AssignedClassesAsync(yearId) };
		if (!result.Options.AcademicYears.Any(y => y.Id == yearId)) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		if (!isAdmin)
		{
			var divisionIds = result.AssignedClasses.Select(a => a.DivisionId).ToHashSet();
			result.Options.Divisions = result.Options.Divisions.Where(d => divisionIds.Contains(d.Id)).ToList();
			var standardIds = result.Options.Divisions.Select(d => d.ParentId).ToHashSet();
			result.Options.Standards = result.Options.Standards.Where(s => standardIds.Contains(s.Id)).ToList();
			var mediumIds = result.Options.Standards.Select(s => s.ParentId).ToHashSet();
			result.Options.Mediums = result.Options.Mediums.Where(m => mediumIds.Contains(m.Id)).ToList();
			var boardIds = result.Options.Mediums.Select(m => m.ParentId).ToHashSet();
			result.Options.Boards = result.Options.Boards.Where(b => boardIds.Contains(b.Id)).ToList();
		}
		return result;
	}

	public async Task<PagedResultDto<StudentAttendanceDto>> ReadAsync(AttendanceWorkspaceInput input)
	{
		await AttendanceActionAuthorization.EnsureAsync(permissions, ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View);
		await scope.EnsureCanAccessAcademicYearAsync(input.AcademicYearId, "read attendance workspace");
		var year = await AsyncExecuter.SingleOrDefaultAsync((await years.GetQueryableAsync())
			.Where(y => y.TenantId == CurrentTenant.Id && y.Id == input.AcademicYearId).Select(y => new { y.StartDate, y.EndDate }));
		if (year == null || input.Date.Date < year.StartDate.Date || input.Date.Date > year.EndDate.Date)
			throw new BusinessException(AttendanceOperationErrorCodes.DateOutsideAcademicYear);
		var isAdmin = await access.IsAdminAsync();
		var allowedDivisions = isAdmin ? null : (await access.AssignedClassesAsync(input.AcademicYearId)).Select(a => a.DivisionId).ToArray();
		var page = await directory.GetAsync(new StudentDirectoryRequest {
			AcademicYearId = input.AcademicYearId, Population = "Active", BoardId = input.BoardId, MediumId = input.MediumId,
			StandardId = input.StandardId, DivisionId = input.DivisionId, Search = input.Search, Sorting = input.Sorting,
			SkipCount = input.SkipCount, MaxResultCount = input.MaxResultCount }, permittedDivisionIds: allowedDivisions, includeAttendanceIdentifiers: true);
		var result = page.Items.Select(FromDirectory).ToList();
		// Count and paginate first. Resolve once per class appearing on this page; never run cumulative reports per row.
		foreach (var group in result.Where(r => r.StandardId.HasValue && r.DivisionId.HasValue).GroupBy(r => (r.StandardId!.Value, r.DivisionId!.Value)))
		{
			var roster = await rosters.LoadAsync(input.AcademicYearId, group.Key.Item1, group.Key.Item2, input.Date);
			var members = roster.Members.ToDictionary(m => m.Student.Id);
			foreach (var row in group)
			{
				if (!members.TryGetValue(row.StudentId, out var member)) continue;
				ApplyAttendance(row, member, roster);
			}
		}
		await FillRecordedStatusesAsync(result.Where(r => !r.IsExpected && r.Status == AttendanceStatus.Pending).ToList(), input.AcademicYearId, input.Date);
		return new(page.TotalCount, result);
	}

	public async Task AddDirectoryRowsAsync(List<StudentAttendanceDto> students, Guid yearId, Guid standardId, Guid divisionId, DateTime date)
	{
		var rows = new Dictionary<Guid, StudentAttendanceDto>(students.ToDictionary(s => s.StudentId));
		var skip = 0;
		while (true)
		{
			var page = await directory.GetAsync(new StudentDirectoryRequest {
				AcademicYearId = yearId, StandardId = standardId, DivisionId = divisionId, Population = "Active", SkipCount = skip, MaxResultCount = 100 });
			foreach (var item in page.Items)
			{
				if (!rows.TryGetValue(item.StudentId, out var row))
				{
					row = FromDirectory(item); students.Add(row); rows.Add(row.StudentId, row);
				}
				row.FirstName = item.FirstName; row.MiddleName = item.MiddleName; row.LastName = item.LastName; row.GrNo = item.GrNo;
				row.GrNumber = item.GrNumber ?? item.GrNo?.ToString(System.Globalization.CultureInfo.InvariantCulture);
				row.StandardId = item.StandardId; row.DivisionId = item.DivisionId;
				row.StandardName = row.ClassName = item.StandardName; row.DivisionName = item.DivisionName;
			}
			skip += page.Items.Count;
			if (skip >= page.TotalCount || page.Items.Count == 0) break;
		}
		await FillRecordedStatusesAsync(students.Where(s => !s.IsExpected && s.Status == AttendanceStatus.Pending).ToList(), yearId, date);
	}

	private async Task FillRecordedStatusesAsync(List<StudentAttendanceDto> students, Guid yearId, DateTime date)
	{
		if (students.Count == 0) return;
		var studentIds = students.Select(s => s.StudentId).ToArray();
		var scopedDays = await scope.ApplyCurrentUserAttendanceDayScopeAsync(await days.GetQueryableAsync());
		var query = from entry in await entries.GetQueryableAsync()
			join day in scopedDays on entry.AttendanceDayId equals day.Id
			where day.TenantId == CurrentTenant.Id && entry.TenantId == CurrentTenant.Id && day.AcademicYearId == yearId &&
				day.Date == date.Date && Enumerable.Contains(studentIds, entry.StudentId)
			orderby day.IsSubmitted descending, day.Id, entry.Id
			select new { entry.StudentId, entry.Status, entry.Remarks, entry.Late, entry.HalfDay, entry.ApprovedLeaveId };
		var observations = await AsyncExecuter.ToListAsync(query);
		foreach (var row in students)
		{
			var recorded = observations.FirstOrDefault(e => e.StudentId == row.StudentId && e.Status != AttendanceStatus.Pending);
			if (recorded == null) continue;
			row.Status = recorded.Status; row.Remarks = recorded.Remarks; row.Late = recorded.Late; row.HalfDay = recorded.HalfDay;
			row.ApprovedLeaveId = recorded.ApprovedLeaveId;
		}
	}

	private static StudentAttendanceDto FromDirectory(StudentDirectoryItemDto row) => new() {
		StudentId = row.StudentId, FirstName = row.FirstName, MiddleName = row.MiddleName, LastName = row.LastName,
		GrNo = row.GrNo, GrNumber = row.GrNumber ?? row.GrNo?.ToString(System.Globalization.CultureInfo.InvariantCulture),
		RollNo = row.RollNo, StandardId = row.StandardId, DivisionId = row.DivisionId,
		StandardName = row.StandardName, ClassName = row.StandardName, DivisionName = row.DivisionName,
		IsExpected = false, IsProvisional = true, NotExpectedReason = "No eligible enrollment or active placement for this date" };

	private void ApplyAttendance(StudentAttendanceDto row, AttendanceRosterMember member, AttendanceRoster roster)
	{
		row.EnrollmentId = member.Enrollment.Id; row.RollNo = member.RollNumber;
		row.Status = member.Entry?.Status ?? AttendanceStatus.Pending; row.Remarks = member.Entry?.Remarks;
		row.Late = member.Entry?.Late ?? false; row.HalfDay = member.Entry?.HalfDay ?? false;
		row.IsExpected = member.Expected; row.NotExpectedReason = member.Reason;
		row.ApprovedLeaveId = member.Leave?.Id; row.ScheduleLabel = member.ScheduleLabel;
		row.NeedsCreditCorrection = AttendanceEntryRules.NeedsCreditCorrection(member, roster);
		row.RequiresCorrectionApproval = member.Entry?.Status is AttendanceStatus.Present or AttendanceStatus.Absent &&
			(roster.Day?.IsLocked == true && roster.Options.LockedCorrectionRequiresApproval ||
			 roster.Date < AttendanceSchoolDate.Today(roster.Options, Clock.Now) && roster.Options.PriorDayCorrectionRequiresApproval);
	}
}
