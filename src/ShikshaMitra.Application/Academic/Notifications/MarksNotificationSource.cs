using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(MarksNotificationSource), typeof(INotificationSourceAdapter))]
public sealed class MarksNotificationSource(IRepository<ExamMarksWorkflowState, Guid> states, IExamRepository exams,
	IRepository<Subject, Guid> subjects, ExamNotificationPlacement placements, ExamNotificationAudience audience,
	MarksNotificationAccess access, ICurrentTenant tenant, ITenantStore tenants, IAsyncQueryableExecuter queries)
	: INotificationSourceAdapter, ITransientDependency
{
	internal const string Type = "Academic.Marks";
	internal static IReadOnlyCollection<string> SupportedEvents { get; } = Array.AsReadOnly(new[]
	{
		AcademicNotificationEventKeys.MarksSubmitted, AcademicNotificationEventKeys.MarksChangesRequested
	});

	public IReadOnlyCollection<string> EventKeys => SupportedEvents;

	internal static string? EventFor(ExamMarksWorkflowStatus status) => status switch
	{
		ExamMarksWorkflowStatus.Submitted => AcademicNotificationEventKeys.MarksSubmitted,
		ExamMarksWorkflowStatus.ChangesRequested => AcademicNotificationEventKeys.MarksChangesRequested,
		_ => null
	};

	// PostgreSQL persists the workflow audit stamp at microsecond precision.
	internal static string Revision(DateTime changedAt) => (changedAt.Ticks / TimeSpan.TicksPerMicrosecond).ToString(CultureInfo.InvariantCulture);

	internal Task<ExamMarksWorkflowState?> FindAsync(Guid school, Guid id, CancellationToken token) =>
		states.FindAsync(x => x.Id == id && x.TenantId == school && !x.IsDeleted, cancellationToken: token);

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		if (context.TenantId == Guid.Empty || context.TenantId != tenant.Id || context.SourceId == Guid.Empty ||
			context.SourceType != Type || !EventKeys.Contains(context.EventKey)) return Unavailable();
		var school = context.TenantId;
		var state = await FindAsync(school, context.SourceId, token);
		if (state?.LastModificationTime is not DateTime changedAt || EventFor(state.Status) != context.EventKey) return Unavailable();
		var revision = Revision(changedAt);
		if (!string.IsNullOrEmpty(context.SourceRevision) && context.SourceRevision != revision) return Unavailable();

		var sections = await placements.QueryAsync(school, (await exams.GetQueryableAsync()).Where(x =>
			x.Id == state.ExamId && x.ExamSubjects.Any(paper => paper.SubjectId == state.SubjectId)));
		var section = await queries.SingleOrDefaultAsync(sections.Where(x => x.DivisionId == state.DivisionId), token);
		if (section == null || (context.SourceScope != null && section.Scope != context.SourceScope)) return Unavailable();
		var subjectName = await queries.SingleOrDefaultAsync((await subjects.GetQueryableAsync()).Where(x =>
			x.Id == state.SubjectId && x.TenantId == school && !x.IsDeleted && x.EducationMediumId == section.MediumId)
			.Select(x => x.Name), token);
		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		if (string.IsNullOrWhiteSpace(subjectName) || string.IsNullOrWhiteSpace(schoolName) ||
			string.IsNullOrWhiteSpace(section.Exam.ExamName)) return Unavailable() with { SourceRevision = revision };

		var recipients = new List<NotificationRecipientRefDto>();
		if (context.EventKey == AcademicNotificationEventKeys.MarksSubmitted)
		{
			if (await access.FindReviewerAsync(school, state, token) is { } reviewer) recipients.Add(reviewer);
		}
		else
		{
			foreach (var teacher in await audience.ReadAssignedTeachersAsync(school, section, state.SubjectId, token))
				if (teacher.UserId is Guid user && await access.CanReceiveAsync(school, state, user, token)) recipients.Add(teacher);
		}
		var variables = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, schoolName),
			["exam_name"] = new(NotificationVariableType.Text, section.Exam.ExamName),
			["subject_name"] = new(NotificationVariableType.Text, subjectName)
		};
		const string link = "/academic/gradeEntry";
		return new(true, null, revision, link, variables, recipients, AudienceSlices: [new(section.Scope, recipients)]);
	}
}
