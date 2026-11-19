using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using P = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students;

namespace ShikshaMitra.Students.Dashboard;

public sealed record StudentModuleDashboardFilter(Guid YearId, Guid? BoardId, Guid? MediumId, Guid? StandardId, Guid? DivisionId)
{
	public string Link(string path, string? preset = null) => path + (path.Contains('?') ? "&" : "?") + $"year={YearId:D}" +
		(BoardId.HasValue ? $"&board={BoardId:D}" : "") + (MediumId.HasValue ? $"&medium={MediumId:D}" : "") +
		(StandardId.HasValue ? $"&class={StandardId:D}" : "") + (DivisionId.HasValue ? $"&division={DivisionId:D}" : "") +
		(preset == null ? "" : "&dashboard=" + preset);
}

[RemoteService(false)]
public partial class StudentModuleDashboardAdminReader(StudentDirectoryQueryService directory, StudentRecordsEnrollmentScopeService enrollmentScope,
	StudentDirectoryOptionsService options, StudentOnboardingAppService onboarding, StudentRecordChangeAppService changes,
	AdmissionApplicationScopeAccessService applicationScope, AdmissionInquiryScopeAccessService inquiryScope,
	IRepository<AdmissionCycle, Guid> cycles, IRepository<AdmissionOffering, Guid> offerings,
	IRepository<AdmissionApplication, Guid> applications, IRepository<AdmissionInquiryFollowUp, Guid> followUps,
	IRepository<StudentRecordChange, Guid> recordChanges, IStudentRepository students) : ApplicationService
{
	public async Task FillAsync(StudentModuleDashboardSectionDto result, StudentModuleDashboardContextDto context, StudentModuleDashboardFilter filter)
	{
		switch (result.Key)
		{
			case "records":
				if (context.Links.Any(l => l.Id == "directory")) await FillRecordsAsync(result, filter);
				if (context.Links.Any(l => l.Id == "changes")) await FillChangesAsync(result, filter);
				break;
			case "admissions": await FillAdmissionsAsync(result, context, filter); break;
			case "attendance": await FillAttendanceAsync(result, context, filter); break;
		}
	}

	private async Task<IQueryable<StudentEnrollment>> EnrollmentsAsync(StudentModuleDashboardFilter filter)
	{
		var query = await enrollmentScope.FilterHierarchyAsync(await enrollmentScope.GetQueryableAsync(), filter.BoardId, filter.MediumId);
		return query.Where(e => e.AcademicYearId == filter.YearId && (!filter.StandardId.HasValue || e.StandardId == filter.StandardId) &&
			(!filter.DivisionId.HasValue || e.DivisionId == filter.DivisionId));
	}

	private static StudentModuleDashboardMetricDto Metric(string id, string label, int value, string href, string permission, string tone = "neutral") =>
		new() { Id = id, Label = label, Value = value, Href = href, Permission = permission, Tone = value == 0 ? "neutral" : tone };

	private async Task FillRecordsAsync(StudentModuleDashboardSectionDto result, StudentModuleDashboardFilter filter)
	{
		var query = await directory.GetPopulationAsync(new StudentDirectoryRequest { AcademicYearId = filter.YearId, Population = "All",
			BoardId = filter.BoardId, MediumId = filter.MediumId, StandardId = filter.StandardId, DivisionId = filter.DivisionId });
		var counts = await AsyncExecuter.ToListAsync(query.GroupBy(s => s.Status).Select(g => new { Status = g.Key, Count = g.Count() }));
		int Count(StudentLifeCycleStatusEnum status) => counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;
		result.Description = "Permanent student records in the selected academic scope.";
		result.Metrics = [Metric("active", "Active students", Count(StudentLifeCycleStatusEnum.Enrolled), filter.Link("/student/records/directory?population=Active", "directory"), P.Records_StudentList.View),
			Metric("inactive", "Inactive students", Count(StudentLifeCycleStatusEnum.InActive), filter.Link("/student/records/directory?population=Former", "directory"), P.Records_StudentList.View),
			Metric("left", "Students who left", Count(StudentLifeCycleStatusEnum.Left), filter.Link("/student/records/directory?population=Former", "directory"), P.Records_StudentList.View)];
		var enrollments = await EnrollmentsAsync(filter);
		var strengths = await AsyncExecuter.ToListAsync(enrollments.Where(e => e.IsActive && query.Any(s => s.Id == e.StudentId && s.Status == StudentLifeCycleStatusEnum.Enrolled))
			.GroupBy(e => new { e.StandardId, e.DivisionId }).Select(g => new { g.Key.StandardId, g.Key.DivisionId, Count = g.Select(e => e.StudentId).Distinct().Count() }));
		var names = await options.GetAsync(filter.YearId);
		var groups = strengths.GroupBy(s => s.StandardId).OrderByDescending(g => g.Sum(s => s.Count)).ThenBy(g => g.Key).ToList();
		result.ClassStrengthTotalCount = groups.Count;
		result.ClassStrength = groups.Take(12).Select(g => new StudentModuleDashboardStrengthDto {
			StandardId = g.Key, Label = names.Standards.FirstOrDefault(s => s.Id == g.Key)?.Name ?? "Unassigned class", Students = g.Sum(s => s.Count),
			Href = g.Key.HasValue ? (filter with { StandardId = g.Key }).Link("/student/records/directory?population=Active", "directory") : "",
			Divisions = g.OrderBy(s => names.Divisions.FirstOrDefault(d => d.Id == s.DivisionId)?.Name).Select(s => new StudentModuleDashboardDivisionStrengthDto {
				DivisionId = s.DivisionId, Label = names.Divisions.FirstOrDefault(d => d.Id == s.DivisionId)?.Name ?? "Unassigned division", Students = s.Count,
				Href = s.DivisionId.HasValue && g.Key.HasValue ? (filter with { StandardId = g.Key, DivisionId = s.DivisionId }).Link("/student/records/directory?population=Active", "directory") : "" }).ToList() }).ToList();
	}

	private async Task FillChangesAsync(StudentModuleDashboardSectionDto result, StudentModuleDashboardFilter filter)
	{
		if (!await AuthorizationService.IsGrantedAsync(P.Records_Changes.Approve)) return;
		var studentQuery = await enrollmentScope.ApplyStudentScopeAsync(await students.GetQueryableWithoutAutoIncludesAsync());
		studentQuery = studentQuery.Where(s => (!filter.BoardId.HasValue || s.AcademicBoardId == filter.BoardId) && (!filter.MediumId.HasValue || s.AcademicMediumId == filter.MediumId) &&
			(!filter.StandardId.HasValue || s.StandardId == filter.StandardId) && (!filter.DivisionId.HasValue || s.DivisionId == filter.DivisionId));
		var query = (await recordChanges.GetQueryableAsync()).Where(c => c.TenantId == CurrentTenant.Id && c.AcademicYearId == filter.YearId && studentQuery.Any(s => s.Id == c.StudentId));
		query = await changes.FilterApprovableAsync(query);
		result.WorkGroups.Add(Metric("recordApprovals", "Record changes awaiting your approval", await AsyncExecuter.CountAsync(query),
			filter.Link("/student/records/changes", "approvable"), P.Records_Changes.View, "warning"));
	}
}
