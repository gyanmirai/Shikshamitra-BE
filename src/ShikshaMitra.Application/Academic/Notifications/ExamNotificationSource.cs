using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(INotificationSourceAdapter))]
public sealed class ExamNotificationSource(IExamRepository exams, ExamNotificationPlacement placements,
	ExamNotificationAudience audience, AcademicNotificationWorkingDays calendar, ICurrentTenant tenant, ITenantStore tenants,
	IClock clock, IAsyncQueryableExecuter queries) : INotificationSourceAdapter, ITransientDependency
{
	internal const string Type = "Academic.Exam";
	internal const string PublicationProperty = "AcademicExamPublicationRevision";
	internal const string Published = AcademicNotificationEventKeys.ExamTimetablePublished;
	internal const string Reminder = AcademicNotificationEventKeys.ExamReminder;
	internal static IReadOnlyCollection<string> SupportedEvents { get; } = Array.AsReadOnly(new[] { Published, Reminder });
	public IReadOnlyCollection<string> EventKeys => SupportedEvents;

	internal static string? PublicationRevision(Exam exam) => exam.GetProperty<string>(PublicationProperty);
	internal static string ReminderRevision(Exam exam) => PublicationRevision(exam) ?? "legacy";

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		if (context.TenantId == Guid.Empty || context.TenantId != tenant.Id || context.SourceId == Guid.Empty ||
			context.SourceType != Type || !EventKeys.Contains(context.EventKey)) return Unavailable();
		var sections = await queries.ToListAsync(await placements.QueryAsync(context.TenantId, (await exams.GetQueryableAsync())
			.Where(x => x.Id == context.SourceId && x.Status == ExamStatus.Published)), token);
		if (sections.Count == 0) return Unavailable();
		var exam = await exams.FindAsync(context.SourceId, includeDetails: true, cancellationToken: token);
		if (exam == null || exam.TenantId != context.TenantId || exam.Status != ExamStatus.Published) return Unavailable();
		var isReminder = context.EventKey == Reminder;
		var occurrence = context.DueOccurrenceAt;
		if (isReminder && (!occurrence.HasValue || context.SourceScope == null)) return Unavailable();
		var revision = isReminder ? ReminderRevision(exam) : PublicationRevision(exam);
		if (string.IsNullOrWhiteSpace(revision) || (!string.IsNullOrEmpty(context.SourceRevision) && context.SourceRevision != revision))
			return Unavailable();

		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		if (context.OccurredAt > now) now = context.OccurredAt;
		var schedule = exam.ExamSubjects.Where(x => x.ExamDate.HasValue && x.StartTime.HasValue && x.EndTime.HasValue &&
			x.StartTime.Value.TimeOfDay < x.EndTime.Value.TimeOfDay).Select(x => new
			{
				x.SubjectId, Date = DateOnly.FromDateTime(x.ExamDate!.Value),
				Start = ExamScheduleStatusResolver.ToScheduledInstant(x.ExamDate!.Value, x.StartTime!.Value),
				End = ExamScheduleStatusResolver.ToScheduledInstant(x.ExamDate!.Value, x.EndTime!.Value)
			}).Where(x => isReminder ? x.Start == occurrence && x.Start > now : x.End > now).ToArray();
		if (schedule.Length == 0) return Unavailable();
		var school = tenant.Name ?? (await tenants.FindAsync(context.TenantId))?.Name;
		if (string.IsNullOrWhiteSpace(school) || string.IsNullOrWhiteSpace(exam.ExamName)) return Unavailable();
		var variables = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, school),
			["exam_name"] = new(NotificationVariableType.Text, exam.ExamName)
		};

		var slices = new List<NotificationSourceAudienceSliceDto>();
		var firstStart = new Dictionary<NotificationRecipientRefDto, DateTimeOffset>();
		var from = schedule.Min(x => x.Date);
		var through = schedule.Max(x => x.Date);
		foreach (var section in sections.Where(x => context.SourceScope == null || x.Scope == context.SourceScope))
		{
			var scope = NotificationRuleScope.Create(section.YearId, section.BoardId, section.MediumId, section.ClassId, section.DivisionId);
			var days = await calendar.ReadWindowAsync(scope, from, through, token);
			if (isReminder && days == null) continue;
			var blocked = days?.CalendarDays.Where(x => CalendarDayBlockingRules.BlocksExamSchedule(x.DayType))
				.Select(x => x.Date).ToHashSet() ?? [];
			var active = schedule.Where(x => !blocked.Contains(x.Date)).ToArray();
			if (active.Length == 0) continue;
			var recipients = await audience.ReadAsync(context.TenantId, section, active.Select(x => x.SubjectId).Distinct().ToArray(), token);
			if (recipients.Count == 0) continue;
			slices.Add(new(section.Scope, recipients.Keys.ToArray()));
			foreach (var (recipient, subjects) in recipients)
			{
				var first = active.Where(x => subjects.Contains(x.SubjectId)).Min(x => x.Start);
				if (!firstStart.TryGetValue(recipient, out var saved) || first < saved) firstStart[recipient] = first;
			}
		}
		if (slices.Count == 0) return Unavailable();
		var content = firstStart.Select(x => new NotificationSourceRecipientContentDto(x.Key,
			new Dictionary<string, NotificationVariableValueDto>(variables, StringComparer.Ordinal)
			{
				["exam_date"] = new(NotificationVariableType.DateTime, x.Value.ToString("O", CultureInfo.InvariantCulture))
			}, Link)).ToArray();
		return new(true, null, revision, Link, variables, firstStart.Keys.ToArray(), occurrence ?? schedule.Max(x => x.End), slices, content);
	}
}
