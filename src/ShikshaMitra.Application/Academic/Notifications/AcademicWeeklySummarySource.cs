using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(INotificationSourceAdapter), typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class AcademicWeeklySummarySource(AcademicNotificationDivisionReader divisions, AcademicWeeklySummaryTiming timing,
	AcademicSummaryBuilder summaries, AcademicWeeklySummaryStaff staff, ClassTeacherNotificationAccess scopeAccess,
	IPermissionChecker permissions, ICurrentTenant tenant, ITenantStore tenants, ICurrentUser current,
	NotificationSchoolTimeZone schoolZone)
	: INotificationSourceAdapter, IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { AcademicWeeklySummaryTiming.EventKey });

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		var school = context.TenantId;
		if (school == Guid.Empty || school != tenant.Id || context.SourceType != AcademicWeeklySummaryTiming.SourceType ||
			!EventKeys.Contains(context.EventKey) || context.SourceScope is not { DivisionId: Guid divisionId } scope ||
			context.SourceId != divisionId || context.DueOccurrenceAt is not { } cutoff ||
			await divisions.FindAsync(school, divisionId, token) is not { } division || division.Scope != scope)
			return Unavailable();

		var scheduled = context.IntentId == Guid.Empty
			? await timing.ResolveAsync(scope, context.OccurredAt, token)
			: AcademicWeeklySummaryWindow.Read(context.SourceRevision);
		if (scheduled == null || scheduled.Revision != context.SourceRevision || cutoff != scheduled.Period.End ||
			context.OccurredAt < cutoff || context.OccurredAt >= scheduled.Period.ExpiresAt)
			return Unavailable();
		var window = scheduled.Period;
		var target = context.DeliveryRecipient;
		var totals = await summaries.ReadAsync(school, scope, window, token, target?.StudentId);
		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		if (totals is not { Count: > 0 } || string.IsNullOrWhiteSpace(schoolName)) return Unavailable();

		var zone = await schoolZone.GetAsync(token);
		var localStart = TimeZoneInfo.ConvertTime(window.Start, zone);
		var localEnd = TimeZoneInfo.ConvertTime(window.End, zone);
		var family = totals.Where(x => target == null || target.StudentId == x.Key).OrderBy(x => x.Key).Select(x =>
		{
			var student = new NotificationRecipientRefDto("Student", StudentId: x.Key);
			return new NotificationSourceRecipientContentDto(student, Variables(x.Value), Link);
		}).ToArray();
		var readers = target?.Kind == "Student" ? [] : await staff.ResolveAsync(school, scope, token);
		if (target != null) readers = readers.Where(x => x == target).ToArray();
		var recipients = family.Select(x => x.Recipient).Concat(readers).Distinct().ToArray();
		if (recipients.Length == 0) return Unavailable();
		var aggregate = new AcademicSummaryCounts(totals.Values.Sum(x => x.Homework), totals.Values.Sum(x => x.Assignments));
		return new(true, null, context.SourceRevision, Link, Variables(aggregate), recipients, window.ExpiresAt,
			[new(scope, recipients)], family);

		Dictionary<string, NotificationVariableValueDto> Variables(AcademicSummaryCounts count) => new(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, schoolName),
			["class_name"] = new(NotificationVariableType.Text, division.Name),
			["summary_period"] = new(NotificationVariableType.Text,
				FormattableString.Invariant($"{localStart:dd MMM yyyy} to {localEnd:dd MMM yyyy}")),
			["summary_text"] = new(NotificationVariableType.Text,
				FormattableString.Invariant($"Homework submissions: {count.Homework}. Assignment submissions: {count.Assignments}.")),
			["homework_submission_count"] = new(NotificationVariableType.Number, count.Homework.ToString(CultureInfo.InvariantCulture)),
			["assignment_submission_count"] = new(NotificationVariableType.Number, count.Assignments.ToString(CultureInfo.InvariantCulture))
		};
	}

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (!EventKeys.Contains(eventKey) || tenant.Id is not Guid school || current.Id is not Guid user ||
			!await permissions.IsGrantedAsync(AcademicWeeklySummaryStaff.View) ||
			await divisions.FindAsync(school, sourceId, token) is not { } division)
			throw new AbpAuthorizationException();
		await scopeAccess.EnsureScopeAsync(school, user, division.Scope, token);
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) => throw new AbpAuthorizationException();
}
