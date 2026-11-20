using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using static ShikshaMitra.Academic.Notifications.AcademicSettingsImpactNotificationSource;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class AcademicSettingsImpactNotificationService(AcademicSettingsImpactNotificationSource source,
	TimetableNotificationSource timetableSource, ITimetableRepository timetables, IRepository<NotificationRule, Guid> ruleRows,
	NotificationEffectiveRuleResolver rules, INotificationIntentWriter writer, IUserScopeService scopes, ICurrentTenant tenant,
	IAsyncQueryableExecuter queries, IClock clock, ICancellationTokenProvider cancellation) : ITransientDependency
{
	internal async Task<IReadOnlyList<SettingsTimetableImpact>> PrepareWeeklyLectureUpdateAsync(WeeklyLectureSlot plan,
		IReadOnlyCollection<WeeklyLectureSlotSubject> previous, Guid? actorId)
	{
		if (tenant.Id is not Guid school || plan.TenantId != school) return [];
		var token = cancellation.Token;
		var saved = SettingsTimetableImpact.Read(plan).GroupBy(x => x.TimetableId)
			.Where(x => x.Count() == 1).ToDictionary(x => x.Key, x => x.Single());
		var hasRules = actorId.HasValue && await queries.AnyAsync((await ruleRows.GetQueryableAsync()).Where(x =>
			x.TenantId == school && !x.IsDeleted && x.EventKey == Event && x.IsEnabled), token);
		if (!hasRules && saved.Count == 0) return [];

		var subjects = await source.ReadPublishedAsync(school, plan.StandardId, token);
		var before = TimetableWeeklyLectureRequirements.Effective(previous, subjects);
		var after = TimetableWeeklyLectureRequirements.Effective(plan.Subjects, subjects);
		if (Ordered(before).SequenceEqual(Ordered(after))) return [];
		var actor = hasRules ? await scopes.GetUserScopeAsync(actorId!.Value) : null;
		var pending = new List<SettingsTimetableImpact>();
		var retained = new List<SettingsTimetableImpact>();
		foreach (var timetable in (await timetables.GetByStandardIdAsync(plan.StandardId)).Where(x =>
			x.TenantId == school && !x.IsDeleted && x.Status == TimetableStatus.Published))
		{
			if (TimetableWeeklyLectureRequirements.Matches(timetable, after)) continue;
			var hash = TimetableNotificationTransition.Fingerprint(TimetableNotificationTransition.Schedule(timetable));
			if (saved.TryGetValue(timetable.Id, out var existing) && existing.ScheduleFingerprint == hash)
			{
				retained.Add(existing);
				continue;
			}
			if (actor == null || actor.TenantId != school || !TimetableWeeklyLectureRequirements.Matches(timetable, before) ||
				await timetableSource.FindScopeAsync(school, timetable.Id, token) is not { } placement ||
				!StudentRecordScope.HasAccess(placement, actor) ||
				await rules.ResolveAsync(Event, placement.RuleScope, token) is not { IsEnabled: true }) continue;
			var impact = new SettingsTimetableImpact(timetable.Id, Guid.NewGuid().ToString("N"), hash);
			retained.Add(impact);
			pending.Add(impact);
		}
		SettingsTimetableImpact.Save(plan, retained);
		return pending;

		static IEnumerable<(Guid?, Guid?, int)> Ordered(IEnumerable<WeeklyLectureSlotSubject> requirements) =>
			requirements.OrderBy(x => x.SubjectId).ThenBy(x => x.ElectiveGroupId).ThenBy(x => x.WeeklyLectures)
				.Select(x => (x.SubjectId, x.ElectiveGroupId, x.WeeklyLectures));
	}

	internal async Task CaptureAsync(IReadOnlyList<SettingsTimetableImpact> impacts, Guid? actorId)
	{
		var at = new DateTimeOffset(clock.Now.ToUniversalTime());
		foreach (var impact in impacts)
			await writer.WriteAsync(new(Event, AcademicSettingsImpactNotificationSource.Type, impact.TimetableId, impact.Revision, at, [], actorId), cancellation.Token);
	}
}
