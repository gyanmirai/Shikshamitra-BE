using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(INotificationDueSource))]
public sealed class ExamReminderDueSource(IExamRepository exams, ExamNotificationPlacement placement,
	IRepository<NotificationRule, Guid> rules, IAsyncQueryableExecuter queries, ICurrentTenant tenant)
	: INotificationDueSource, ITransientDependency
{
	public string SourceType => ExamNotificationSource.Type;
	public IReadOnlyCollection<string> ScheduledEventKeys { get; } = Array.AsReadOnly(new[] { ExamNotificationSource.Reminder });

	public async Task<NotificationDueSourcePage> ReadPageAsync(NotificationDueScanRequest request, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		if (request.TenantId == Guid.Empty || request.TenantId != tenant.Id) return new([], null);
		if (request.PageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(request));
		var enabled = (await rules.GetQueryableAsync()).Where(x => x.TenantId == request.TenantId && !x.IsDeleted &&
			x.IsEnabled && x.EventKey == ExamNotificationSource.Reminder);
		if (!await queries.AnyAsync(enabled, token)) return new([], null);

		var today = DateTime.SpecifyKind(ExamScheduleStatusResolver.GetInstitutionNow(request.Now.UtcDateTime).Date, DateTimeKind.Utc);
		var sections = await placement.QueryAsync(request.TenantId,
			(await exams.GetQueryableAsync()).Where(x => x.Status == ExamStatus.Published));
		var scoped = from section in sections
			from subject in section.Exam.ExamSubjects
			where subject.ExamDate >= today && subject.StartTime.HasValue && subject.EndTime.HasValue &&
				enabled.Any(rule => (!rule.AcademicYearId.HasValue || rule.AcademicYearId == section.YearId) &&
					(!rule.BoardId.HasValue || rule.BoardId == section.BoardId) &&
					(!rule.MediumId.HasValue || rule.MediumId == section.MediumId) &&
					(!rule.ClassId.HasValue || rule.ClassId == section.ClassId) &&
					(!rule.DivisionId.HasValue || rule.DivisionId == section.DivisionId))
			select new { section, SubjectId = subject.Id, subject.ExamDate, subject.StartTime, subject.EndTime };
		if (request.Continuation is { } cursor)
		{
			var (subjectId, divisionId) = LearningTaskReminderDuePager.ReadCursor(cursor);
			scoped = scoped.Where(x => x.SubjectId.CompareTo(subjectId) > 0 ||
				x.SubjectId == subjectId && x.section.DivisionId.CompareTo(divisionId) > 0);
		}
		var page = await queries.ToListAsync(scoped.OrderBy(x => x.SubjectId).ThenBy(x => x.section.DivisionId).Take(request.PageSize + 1), token);
		var candidates = new List<NotificationDueSourceCandidate>();
		foreach (var row in page.Take(request.PageSize))
		{
			if (row.StartTime!.Value.TimeOfDay >= row.EndTime!.Value.TimeOfDay) continue;
			var start = ExamScheduleStatusResolver.ToScheduledInstant(row.ExamDate!.Value, row.StartTime.Value);
			if (start <= request.Now) continue;
			candidates.Add(new(ExamNotificationSource.Reminder, row.section.Exam.Id, ExamNotificationSource.ReminderRevision(row.section.Exam),
				row.section.Scope, start, start, NotificationDueTimeMode.PrecedingSchoolDays));
		}
		var next = page.Count <= request.PageSize ? null
			: $"{page[request.PageSize - 1].SubjectId:N}:{page[request.PageSize - 1].section.DivisionId:N}";
		return new(candidates.Distinct().ToArray(), next);
	}
}
