using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using ClassTeacherEntity = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;

namespace ShikshaMitra.Academic.Dashboard;

[RemoteService(false)]
public class AcademicInsightsTeachingReader(IRepository<ClassTeacherEntity, Guid> classTeachers,
	IRepository<AssignedSubject, Guid> subjects, IRepository<ChapterProgress, Guid> progress,
	IRepository<AssignedSubjectChapter, Guid> chapters, IRepository<LessonPlan, Guid> lessons,
	IRepository<Timetable.Timetables.Timetable, Guid> timetables, IRepository<Exam, Guid> exams,
	IRepository<Employee, Guid> employees, LessonPlanAppService lessonAccess) : ApplicationService
{
	public static int ToDayOfWeek(int sourceDay) => (sourceDay + 1) % 7;
	public async Task<AcademicInsightSectionDto> ClassroomsAsync(AcademicInsightsContext c)
	{
		var ids = c.DivisionIds;
		var mapped = await AsyncExecuter.ToListAsync(AcademicInsightsClassTeacherQuery.ForContext(await classTeachers.GetQueryableAsync(), c)
			.Select(x => x.DivisionId).Distinct());
		return new() { Key = "classrooms", Status = ids.Length == 0 ? "empty" : "ready", Summary = "Active class teacher mappings. Metrics cover the full selected scope; the chart shows up to 120 divisions.",
			Metrics = [new("divisions", "Divisions", ids.Length), new("mapped", "Mapped divisions", mapped.Count)],
			Series = [new("class-mapping", c.Divisions.OrderBy(x => x.Label).Take(120).Select(x => new AcademicInsightPointDto(x.Id.ToString(), x.Label,
				mapped.Contains(x.Id) ? 1 : 0, Status: mapped.Contains(x.Id) ? "mapped" : "unmapped")).ToList())] };
	}
	public async Task<AcademicInsightSectionDto> CurriculumAsync(AcademicInsightsContext c)
	{
		var divisions = c.DivisionIds; var standards = c.StandardIds;
		var progressQuery = await progress.GetQueryableAsync();
		var latestIds = LatestProgressIds(progressQuery.Where(x => Enumerable.Contains(divisions, x.DivisionId)));
		var query = from p in progressQuery
			join chapter in await chapters.GetQueryableAsync() on p.ChapterId equals chapter.Id
			join subject in await subjects.GetQueryableAsync() on chapter.AssignedSubjectId equals subject.Id
			where chapter.TenantId == c.TenantId && subject.TenantId == c.TenantId && latestIds.Contains(p.Id) && Enumerable.Contains(standards, subject.StandardId)
			select new { subject.Id, subject.SubjectCode, p.Progress };
		var rows = await AsyncExecuter.ToListAsync(query.GroupBy(x => new { x.Id, x.SubjectCode }).Select(g => new { g.Key.Id, g.Key.SubjectCode, Average = g.Average(x => (double)x.Progress), Count = g.Count() }));
		return new() { Key = "curriculum", Status = rows.Count == 0 ? "empty" : "ready", Summary = "Recorded chapter progress only. Metrics cover all records; the chart shows up to 30 subjects. No planned target is inferred.",
			Metrics = [new("recorded-progress", "Average recorded progress", rows.Count == 0 ? null : rows.Sum(x => x.Average * x.Count) / rows.Sum(x => x.Count), "%")],
			Series = [new("recorded-progress", rows.OrderBy(x => x.SubjectCode).Take(30).Select(x => new AcademicInsightPointDto(x.Id.ToString(), x.SubjectCode, Math.Round(x.Average, 1))).ToList())] };
	}
	internal static IQueryable<Guid> LatestProgressIds(IQueryable<ChapterProgress> query) => query.GroupBy(x => new { x.ChapterId, x.DivisionId })
		.Select(g => g.OrderByDescending(x => x.CreationTime).ThenByDescending(x => x.Id).Select(x => x.Id).First());
	public async Task<AcademicInsightSectionDto> LessonsAsync(AcademicInsightsContext c)
	{
		var ids = c.DivisionIds;
		var ownEmployees = (await employees.GetQueryableAsync()).Where(x => x.TenantId == c.TenantId && x.UserId == CurrentUser.Id).Select(x => x.Id);
		var classDivisions = await AsyncExecuter.ToListAsync(AcademicInsightsClassTeacherQuery.ForContext(await classTeachers.GetQueryableAsync(), c)
			.Where(x => ownEmployees.Contains(x.TeacherId)).Select(x => x.DivisionId).Distinct());
		var query = (await lessons.GetQueryableAsync()).Where(x => x.TenantId == c.TenantId && x.AcademicYearId == c.Input.AcademicYearId && Enumerable.Contains(ids, x.DivisionId));
		query = await lessonAccess.ApplyInsightsOwnerScopeAsync(query, c.Scope.IsAdmin, classDivisions.ToArray());
		var rows = await AsyncExecuter.ToListAsync(query.GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() }));
		return new() { Key = "lesson", Status = rows.Count == 0 ? "empty" : "ready", Summary = "Lesson plan review states for your own/reporting teachers and exact active class teacher assignments within the selected scope.",
			Series = [new("status", rows.Select(x => new AcademicInsightPointDto(x.Status.ToString(), x.Status.ToString(), x.Count)).ToList())],
			Metrics = [new("plans", "Lesson plans", rows.Sum(x => x.Count))] };
	}
	public async Task<AcademicInsightSectionDto> TimetableAsync(AcademicInsightsContext c)
	{
		var ids = c.DivisionIds;
		var query = (await timetables.GetQueryableAsync()).Where(x => x.TenantId == c.TenantId && Enumerable.Contains(ids, x.DivisionId));
		var count = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.SelectMany(x => x.Slots).GroupBy(x => new { x.Day, x.Sequence }).Select(g => new { g.Key.Day, g.Key.Sequence, Count = g.Count() }));
		return new() { Key = "timetable", Status = count == 0 ? "empty" : "ready", Summary = "Scheduled periods across assigned divisions; cells count slots, not timetable conflicts.",
			Metrics = [new("timetables", "Timetables", count)], Series = [new("periods", rows.OrderBy(x => x.Day).ThenBy(x => x.Sequence).Select(x =>
				new AcademicInsightPointDto($"{x.Day}:{x.Sequence}", $"{x.Day} / {x.Sequence}", x.Count, ToDayOfWeek((int)x.Day), x.Sequence)).ToList())] };
	}
	public async Task<AcademicInsightSectionDto> ExamsAsync(AcademicInsightsContext c)
	{
		var standards = c.StandardIds; var divisions = c.DivisionIds;
		var rows = await AsyncExecuter.ToListAsync((await exams.GetQueryableAsync()).Where(x => x.TenantId == c.TenantId && Enumerable.Contains(standards, x.StandardId) &&
			x.ExamDivisions.Any(d => Enumerable.Contains(divisions, d.DivisionId))).GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() }));
		return new() { Key = "exams", Status = rows.Count == 0 ? "empty" : "ready", Summary = "Exam stages for assigned classes and divisions.",
			Metrics = [new("exams", "Exams", rows.Sum(x => x.Count))], Series = [new("stages", rows.Select(x => new AcademicInsightPointDto(x.Status.ToString(), x.Status.ToString(), x.Count)).ToList())] };
	}
}
