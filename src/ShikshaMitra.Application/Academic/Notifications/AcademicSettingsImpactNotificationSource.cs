using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record SettingsTimetableImpact(Guid TimetableId, string Revision, string ScheduleFingerprint)
{
	private const string Property = "AcademicWeeklyLectureImpact";

	internal static SettingsTimetableImpact[] Read(WeeklyLectureSlot plan)
	{
		var json = plan.GetProperty<string>(Property);
		if (string.IsNullOrWhiteSpace(json)) return [];
		try { return JsonSerializer.Deserialize<SettingsTimetableImpact[]>(json) ?? []; }
		catch (JsonException) { return []; }
	}

	internal static void Save(WeeklyLectureSlot plan, IReadOnlyList<SettingsTimetableImpact> impacts)
	{
		if (impacts.Count == 0) plan.RemoveProperty(Property);
		else plan.SetProperty(Property, JsonSerializer.Serialize(impacts.OrderBy(x => x.TimetableId)));
	}
}

[RemoteService(false), ExposeServices(typeof(AcademicSettingsImpactNotificationSource), typeof(INotificationSourceAdapter),
	typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class AcademicSettingsImpactNotificationSource(TimetableNotificationSource timetableSource, ITimetableRepository timetables,
	IWeeklyLectureSlotRepository weeklyPlans, IRepository<AssignedSubject, Guid> subjects, TimetableNotificationAudience staff,
	AcademicNotificationStaffAccess staffAccess, IConfiguration configuration, IAsyncQueryableExecuter queries,
	IUserScopeAccessService scopeAccess, IPermissionChecker permissions, ICurrentTenant tenant, ITenantStore tenants)
	: INotificationSourceAdapter, IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	internal const string Event = AcademicNotificationEventKeys.SettingsOperationalImpact;
	internal const string Type = "Academic.Settings";
	internal const string View = ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View;
	private const string TimetableWorkspace = "/academic/newTimetable";
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { Event });

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		var school = context.TenantId;
		if (school == Guid.Empty || tenant.Id != school || context.EventKey != Event || context.SourceType != Type ||
			await timetableSource.FindScopeAsync(school, context.SourceId, token) is not { } placement ||
			context.SourceScope != null && context.SourceScope != placement.Scope ||
			await weeklyPlans.FindByStandardIdAsync(placement.StandardId) is not { } plan || plan.TenantId != school)
			return LearningTaskNotificationContent.Unavailable();

		var impact = SettingsTimetableImpact.Read(plan).FirstOrDefault(x => x.TimetableId == context.SourceId &&
			x.Revision == context.SourceRevision);
		if (impact == null) return LearningTaskNotificationContent.Unavailable();
		var timetable = await timetables.FindByStandardAndDivisionAsync(placement.StandardId, placement.DivisionId);
		if (timetable == null || timetable.TenantId != school || timetable.Id != context.SourceId || timetable.Status != TimetableStatus.Published)
			return LearningTaskNotificationContent.Unavailable();

		var schedule = TimetableNotificationTransition.Schedule(timetable);
		if (TimetableNotificationTransition.Fingerprint(schedule) != impact.ScheduleFingerprint)
			return LearningTaskNotificationContent.Unavailable();
		var requirements = TimetableWeeklyLectureRequirements.Effective(plan.Subjects, await ReadPublishedAsync(school, plan.StandardId, token));
		if (TimetableWeeklyLectureRequirements.Matches(timetable, requirements)) return LearningTaskNotificationContent.Unavailable();

		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		var expiry = await timetableSource.ResolveExpiryAsync(placement, token);
		if (string.IsNullOrWhiteSpace(schoolName) || expiry == null || expiry <= context.OccurredAt)
			return LearningTaskNotificationContent.Unavailable();

		var scheduled = schedule.Where(x => x.TeacherId.HasValue).Select(x => x.TeacherId!.Value).Distinct().ToArray();
		var candidates = (await staff.FindTeachersAsync(school, scheduled, token)).ToList();
		if (Guid.TryParse(configuration[$"Notifications:Academic:Tenants:{school:N}:SettingsImpactReviewerUserId"], out var reviewer)
			&& reviewer != Guid.Empty)
			candidates.Add(new("ConfiguredReviewer", UserId: reviewer));

		if (context.DeliveryRecipient != null)
			candidates = candidates.Where(x => x == context.DeliveryRecipient).ToList();
		var recipients = await staffAccess.FilterAsync(school, placement.Scope, candidates, View, token);
		if (recipients.Count == 0) return LearningTaskNotificationContent.Unavailable();
		var values = new Dictionary<string, NotificationVariableValueDto>
		{
			["school_name"] = new(NotificationVariableType.Text, schoolName),
			["class_name"] = new(NotificationVariableType.Text, placement.ClassName),
			["setting_name"] = new(NotificationVariableType.Text, "Weekly lecture requirements"),
			["impact_summary"] = new(NotificationVariableType.Text,
				"The published timetable needs review because it no longer matches the weekly lecture requirements.")
		};
		return new(true, null, context.SourceRevision, TimetableWorkspace, values, recipients, expiry, [new(placement.Scope, recipients)]);
	}

	internal async Task<IReadOnlyCollection<AssignedSubject>> ReadPublishedAsync(Guid school, Guid standardId, CancellationToken token) =>
		await queries.ToListAsync((await subjects.GetQueryableAsync()).Where(x =>
			x.TenantId == school && !x.IsDeleted && x.StandardId == standardId && x.Status == SubjectStatus.Published), token);

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (eventKey != Event || tenant.Id is not Guid school || !await permissions.IsGrantedAsync(View) ||
			await timetableSource.FindScopeAsync(school, sourceId, token) is not { } placement ||
			!StudentRecordScope.HasAccess(placement, await scopeAccess.GetCurrentUserScopeAsync()))
			throw new AbpAuthorizationException();
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) => throw new AbpAuthorizationException();
}
