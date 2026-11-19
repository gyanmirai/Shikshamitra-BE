using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Timing;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class TimetableNotificationDelivery(TimetableNotificationSource source, NotificationEffectiveRuleResolver rules,
	INotificationIntentWriter writer, ITimetableRepository timetables, ICurrentTenant tenant, IClock clock,
	ICancellationTokenProvider cancellation) : ITransientDependency
{
	internal async Task CaptureReassignmentsAsync(IReadOnlyList<TimetableTeacherReassignment> changes, Guid? actorId)
	{
		foreach (var change in changes)
		{
			var timetable = change.Timetable;
			if (timetable.Status != TimetableStatus.Published) continue;
			var previous = TimetableNotificationTransition.Schedule(timetable, change.PreviousTeachers);
			var transition = TimetableNotificationTransition.Record(timetable, TimetableStatus.Published, previous);
			if (transition == null) continue;
			await timetables.UpdateAsync(timetable, autoSave: true, cancellationToken: cancellation.Token);
			await CaptureAsync(timetable.Id, transition, actorId);
		}
	}

	internal async Task CaptureAsync(Guid timetableId, TimetableNotificationTransition? transition, Guid? actorId)
	{
		if (transition == null || tenant.Id is not Guid tenantId || actorId is not Guid actor || actor == Guid.Empty) return;
		var token = cancellation.Token;
		var placement = await source.FindScopeAsync(tenantId, timetableId, token);
		if (placement == null) return;
		var scope = placement.Scope;
		var rule = await rules.ResolveAsync(transition.EventKey, NotificationRuleScope.Create(scope.AcademicYearId,
			scope.BoardId, scope.MediumId, scope.ClassId, scope.DivisionId), token);
		if (rule is not { IsEnabled: true }) return;
		await writer.WriteAsync(new(transition.EventKey, TimetableNotificationSource.Type, timetableId, transition.Revision,
			new DateTimeOffset(clock.Now.ToUniversalTime()), [], actor), token);
	}
}
