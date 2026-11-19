using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Dashboard;

[RemoteService(false)]
public class AcademicInsightsTaskReader(IRepository<HomeworkAssignmentEntity, Guid> homework,
	IRepository<AssignmentEntity, Guid> assignments, IRepository<StudentSubmissionEntity, Guid> submissions, HomeworkAppService homeworkAccess) : ApplicationService
{
	public async Task<AcademicInsightSectionDto> TasksAsync(AcademicInsightsContext c)
	{
		var divisions = c.DivisionIds; var standards = c.StandardIds; var year = c.Input.AcademicYearId;
		var hw = await homeworkAccess.ApplyCurrentUserScopeAsync((await homework.GetQueryableAsync()).Where(x => x.TenantId == c.TenantId && Enumerable.Contains(standards, x.StandardId) && (!x.AcademicYearId.HasValue || x.AcademicYearId == year)));
		var assignment = AssignmentV2AppService.ApplyAssignmentScope((await assignments.GetQueryableAsync()).Where(x => x.TenantId == c.TenantId && Enumerable.Contains(standards, x.StandardId) && (!x.AcademicYearId.HasValue || x.AcademicYearId == year)), c.Scope);
		var scheduled = hw.SelectMany(x => x.DivisionSchedules).Where(x => x.TenantId == c.TenantId && x.IsPublished && Enumerable.Contains(divisions, x.DivisionId)).Select(x => x.DueDate);
		var legacy = hw.Where(x => x.DivisionId.HasValue && Enumerable.Contains(divisions, x.DivisionId.Value) && x.IsPublished && x.DueDate.HasValue &&
			!x.DivisionSchedules.Any()).Select(x => x.DueDate!.Value);
		var hwSeries = await DueSeriesAsync("homework", scheduled.Concat(legacy));
		var assignmentSeries = await DueSeriesAsync("assignment", assignment.SelectMany(x => x.DivisionSchedules).Where(x => x.TenantId == c.TenantId && x.IsPublished && Enumerable.Contains(divisions, x.DivisionId)).Select(x => x.DueDate));
		var total = hwSeries.Points.Sum(x => x.Value) + assignmentSeries.Points.Sum(x => x.Value);
		return new() { Key = "tasks", Status = total == 0 ? "empty" : "ready", Summary = "Published homework and assignment division schedules by due date. Overdue describes the schedule, not missing submissions.",
			Metrics = [new("published-schedules", "Published task schedules", total)], Series = [hwSeries, assignmentSeries] };
	}
	private async Task<AcademicInsightSeriesDto> DueSeriesAsync(string name, IQueryable<DateTime> query)
	{
		var today = Mobile.MobileAcademicContextAppService.SchoolToday; var tomorrow = today.AddDays(1);
		var rows = await AsyncExecuter.ToListAsync(query.GroupBy(x => x < today ? "overdue" : x < tomorrow ? "today" : "upcoming").Select(g => new { Key = g.Key, Count = g.Count() }));
		return new(name, rows.Select(x => new AcademicInsightPointDto(x.Key, x.Key, x.Count)).ToList());
	}
	public async Task<AcademicInsightSectionDto> EvaluationAsync(AcademicInsightsContext c)
	{
		var divisions = c.DivisionIds; var standards = c.StandardIds; var year = c.Input.AcademicYearId;
		var hw = (await homeworkAccess.ApplyCurrentUserScopeAsync((await homework.GetQueryableAsync()).Where(x => x.TenantId == c.TenantId && Enumerable.Contains(standards, x.StandardId) && (!x.AcademicYearId.HasValue || x.AcademicYearId == year)))).Select(x => x.Id);
		var assignment = AssignmentV2AppService.ApplyAssignmentScope((await assignments.GetQueryableAsync()).Where(x => x.TenantId == c.TenantId && Enumerable.Contains(standards, x.StandardId) && (!x.AcademicYearId.HasValue || x.AcademicYearId == year)), c.Scope).Select(x => x.Id);
		var rows = await AsyncExecuter.ToListAsync((await submissions.GetQueryableAsync()).Where(x => x.TenantId == c.TenantId && Enumerable.Contains(divisions, x.DivisionId) && x.SubmissionDate.HasValue &&
			(x.HomeworkAssignmentId.HasValue && hw.Contains(x.HomeworkAssignmentId.Value) || x.AssignmentId.HasValue && assignment.Contains(x.AssignmentId.Value)))
			.GroupBy(x => x.ReviewedAt.HasValue || x.GradedDate.HasValue).Select(g => new { Reviewed = g.Key, Count = g.Count() }));
		return new() { Key = "evaluation", Status = rows.Count == 0 ? "empty" : "ready", Summary = "Received submissions grouped only by recorded review timestamps. No recorded review does not imply evaluation is required or pending.",
			Metrics = [new("submissions", "Received submissions", rows.Sum(x => x.Count))],
			Series = [new("review", rows.Select(x => new AcademicInsightPointDto(x.Reviewed ? "reviewed" : "no-recorded-review", x.Reviewed ? "Recorded review" : "No recorded review", x.Count)).ToList())] };
	}
}
