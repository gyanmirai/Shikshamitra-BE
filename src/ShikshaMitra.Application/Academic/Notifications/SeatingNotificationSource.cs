using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.SeatAllocation;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(INotificationSourceAdapter))]
public sealed class SeatingNotificationSource(IRepository<ExamSeatingPublication, Guid> publications,
	IRepository<ExamSeatingPlan, Guid> plans, IRepository<ExamSeatingPlanSubject, Guid> planSubjects,
	IRepository<ExamSeatAllocation, Guid> seats, IExamRepository exams, ExamNotificationPlacement placements,
	ExamNotificationAudience audience, AcademicNotificationWorkingDays calendar,
	ICurrentTenant tenant, ITenantStore tenants, IClock clock, IAsyncQueryableExecuter queries)
	: INotificationSourceAdapter, ITransientDependency
{
	internal const string Published = AcademicNotificationEventKeys.SeatingPublished;
	internal const string Type = "Academic.Seating";
	internal static IReadOnlyCollection<string> SupportedEvents { get; } = Array.AsReadOnly(new[] { Published });
	public IReadOnlyCollection<string> EventKeys => SupportedEvents;

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		if (context.TenantId == Guid.Empty || context.TenantId != tenant.Id || context.SourceId == Guid.Empty ||
			context.SourceType != Type || context.EventKey != Published) return Unavailable();

		var school = context.TenantId;
		var examId = context.SourceId;
		var release = await publications.FindAsync(x => x.TenantId == school && x.ExamId == examId, cancellationToken: token);
		if (release == null || SeatingNotificationPublication.ReadOptions(release) is not { } options) return Unavailable();
		var revision = release.Id.ToString("N");
		if (!string.IsNullOrEmpty(context.SourceRevision) && context.SourceRevision != revision) return Unavailable();
		var selectors = new List<string>();
		if (options.NotifyParents) selectors.AddRange(["PrimaryGuardian", "AdditionalGuardians"]);
		if (options.NotifyStudents) selectors.Add("StudentAccount");
		if (options.NotifyTeachers) selectors.Add("AssignedTeacher");
		var channels = new List<CommunicationChannelType> { CommunicationChannelType.InApp,
			CommunicationChannelType.WebPush, CommunicationChannelType.WhatsApp };
		if (options.SendEmail) channels.Add(CommunicationChannelType.Email);

		var sections = await queries.ToListAsync(await placements.QueryAsync(school, (await exams.GetQueryableAsync())
			.Where(x => x.Id == examId && x.Status == ExamStatus.Published)), token);
		var exam = sections.Count == 0 ? null : await exams.FindAsync(examId, includeDetails: true, cancellationToken: token);
		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		if (exam == null || exam.TenantId != school || exam.Status != ExamStatus.Published ||
			string.IsNullOrWhiteSpace(schoolName) || string.IsNullOrWhiteSpace(exam.ExamName)) return Unavailable();
		if (selectors.Count == 0) return Unavailable() with { SourceRevision = revision };

		var variables = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, schoolName),
			["exam_name"] = new(NotificationVariableType.Text, exam.ExamName)
		};
		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		if (context.OccurredAt > now) now = context.OccurredAt;
		var schedule = exam.ExamSubjects.Where(x => x.ExamDate.HasValue && x.StartTime.HasValue && x.EndTime.HasValue &&
			x.StartTime.Value.TimeOfDay < x.EndTime.Value.TimeOfDay).Select(x => new
			{
				x.Id, x.SubjectId, Date = DateOnly.FromDateTime(x.ExamDate!.Value),
				End = ExamScheduleStatusResolver.ToScheduledInstant(x.ExamDate!.Value, x.EndTime!.Value)
			}).Where(x => x.End > now).ToDictionary(x => x.Id);
		if (schedule.Count == 0) return Unavailable() with { SourceRevision = revision };

		var publishedPlans = (await plans.GetQueryableAsync()).Where(x => x.TenantId == school && x.ExamId == examId &&
			x.Status == SeatingPlanStatus.Published && x.IsLocked && !x.IsDeleted).Select(x => x.Id);
		var subjectLinks = (await planSubjects.GetQueryableAsync()).Where(x => x.TenantId == school && x.ExamId == examId &&
			publishedPlans.Contains(x.SeatingPlanId));
		var links = (await queries.ToListAsync(subjectLinks, token))
			.Where(x => schedule.TryGetValue(x.ExamSubjectId, out var paper) && paper.SubjectId == x.SubjectId).ToArray();
		if (links.Length == 0) return Unavailable() with { SourceRevision = revision };
		var allocations = options.NotifyParents || options.NotifyStudents
			? await queries.ToListAsync((await seats.GetQueryableAsync()).Where(x => x.TenantId == school && x.ExamId == examId &&
				publishedPlans.Contains(x.SeatingPlanId) && !x.IsDeleted && x.SeatNumber > 0)
				.Select(x => new { x.StudentId, x.RoomId, x.SeatingPlanId, x.SeatNumber }), token)
			: [];
		var byLearner = allocations.ToLookup(x => x.StudentId);
		var dates = schedule.Values.Select(x => x.Date).ToArray();
		var slices = new List<NotificationSourceAudienceSliceDto>();
		var content = new List<NotificationSourceRecipientContentDto>();
		foreach (var section in sections.Where(x => context.SourceScope == null || x.Scope == context.SourceScope))
		{
			var scope = NotificationRuleScope.Create(section.YearId, section.BoardId, section.MediumId, section.ClassId, section.DivisionId);
			var days = await calendar.ReadWindowAsync(scope, dates.Min(), dates.Max(), token);
			var blocked = days?.CalendarDays.Where(x => CalendarDayBlockingRules.BlocksExamSchedule(x.DayType))
				.Select(x => x.Date).ToHashSet() ?? [];
			var sectionLinks = links.Where(x => !blocked.Contains(schedule[x.ExamSubjectId].Date)).ToArray();
			if (sectionLinks.Length == 0) continue;
			var recipients = await audience.ReadAsync(school, section, sectionLinks.Select(x => x.SubjectId).Distinct().ToArray(), token);
			var selected = new List<NotificationRecipientRefDto>();
			foreach (var (recipient, subjectIds) in recipients)
			{
				if (recipient.Kind == "Student" && recipient.StudentId is Guid studentId)
				{
					var applicablePlans = sectionLinks.Where(x => subjectIds.Contains(x.SubjectId))
						.Select(x => x.SeatingPlanId).ToHashSet();
					var assignments = byLearner[studentId].Where(x => applicablePlans.Contains(x.SeatingPlanId))
						.Select(x => (x.RoomId, x.SeatNumber)).Distinct().ToArray();
					if (assignments.Length == 0) continue;
					selected.Add(recipient);
					if (assignments.Length == 1)
						content.Add(new(recipient, new Dictionary<string, NotificationVariableValueDto>(variables, StringComparer.Ordinal)
						{
							["seat_number"] = new(NotificationVariableType.Text, assignments[0].SeatNumber.ToString(CultureInfo.InvariantCulture))
						}, Link));
				}
				else if (options.NotifyTeachers && recipient.Kind == "AssignedTeacher") selected.Add(recipient);
			}
			if (selected.Count > 0) slices.Add(new(section.Scope, selected));
		}

		if (slices.Count == 0) return Unavailable() with { SourceRevision = revision };
		return new(true, null, revision, Link, variables, slices.SelectMany(x => x.Recipients).Distinct().ToArray(),
			schedule.Values.Max(x => x.End), slices, content)
		{
			DeliveryLimits = new(selectors, channels)
		};
	}
}
