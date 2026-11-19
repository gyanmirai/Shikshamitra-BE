using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.ClassManagement.ClassTeacherAssignment;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using ClassTeacherRecord = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record ClassTeacherNoticeState(Guid? ConfirmedEmployeeId, string? EventKey = null, string? Revision = null,
	Guid? ActorId = null, NotificationRecipientRefDto? AssignedTeacher = null, NotificationRecipientRefDto? PreviousTeacher = null);

internal sealed record ClassTeacherNoticeChange(Guid DivisionId, ClassTeacherNoticeState State);

[RemoteService(false)]
public sealed class ClassTeacherNotificationPublication(IRepository<Division, Guid> divisions, IRepository<NotificationRule, Guid> rules,
	TimetableNotificationAudience staff, IAsyncQueryableExecuter queries, INotificationIntentWriter writer, INotificationPersonalWriteLock locks,
	ICurrentTenant tenant, ICancellationTokenProvider cancellation, IClock clock) : ITransientDependency
{
	private const string StateProperty = "AcademicClassTeacherNotificationState";

	internal Task LockAsync(Guid organizationId) => tenant.Id.HasValue
		? locks.AcquireAsync([$"academic-class-teacher:{organizationId:N}"], cancellation.Token)
		: Task.CompletedTask;

	internal static ClassTeacherNoticeState? Read(Division division) =>
		division.GetProperty<string>(StateProperty) is { } saved
			? NotificationRuntimeJson.Deserialize<ClassTeacherNoticeState>(saved, NotificationErrorCodes.SourceUnavailable)
			: null;

	internal async Task<IReadOnlyList<ClassTeacherNoticeChange>> PrepareAsync(SaveClassTeacherAssignmentsDto input, Guid[] standards,
		IReadOnlyList<ClassTeacherRecord> existing, Guid? actor)
	{
		if (tenant.Id is not Guid school || standards.Length == 0) return [];
		var token = cancellation.Token;
		var sections = await queries.ToListAsync((await divisions.GetQueryableAsync()).Where(x =>
			x.TenantId == school && !x.IsDeleted && Enumerable.Contains(standards, x.StandardId) &&
			x.Standard.TenantId == school && x.Standard.EducationMediumStageId == input.OrganizationId &&
			x.Standard.EducationMediumStage.TenantId == school)
			.Select(x => new { Division = x, MediumId = x.Standard.EducationMediumStage.EducationMediumId }), token);
		var active = existing.Where(x => x.TenantId == school && x.Status == ClassTeacherAssignmentStatus.Active)
			.ToLookup(x => (x.StandardId, x.DivisionId, x.AcademicMediumId));
		var incoming = input.Assignments.ToLookup(x => (x.ClassId, x.DivisionId));
		var reads = sections.Select(placement =>
		{
			var section = placement.Division;
			var key = (section.StandardId, section.Id);
			var live = active[(key.StandardId, key.Id, placement.MediumId)].Select(x => (Guid?)x.TeacherId).Distinct().Take(2).ToArray();
			var selected = incoming[key].Select(x => (Guid?)x.TeacherId).Distinct().Take(2).ToArray();
			var saved = Read(section);
			// Confirmed writes recreate the assignment rows. Keep this identity on the division
			// so repeated saves and draft replacements preserve the last confirmed teacher.
			var previous = live.Length == 1 ? live[0] : saved?.ConfirmedEmployeeId;
			return new { Section = section, Saved = saved, Previous = previous, Next = selected.Length == 1 ? selected[0] : null,
				IsConflicting = live.Length > 1 || selected.Length > 1 };
		}).Where(x => !x.IsConflicting).ToArray();
		var changed = reads.Where(x => !input.SaveAsDraft && x.Previous != x.Next).ToArray();
		var teacherIds = changed.SelectMany(x => new[] { x.Previous, x.Next }).OfType<Guid>().Distinct().ToArray();
		var accounts = (await staff.FindTeachersAsync(school, teacherIds, token)).ToDictionary(x => x.EmployeeId!.Value);
		var changes = new List<ClassTeacherNoticeChange>();
		var updated = new List<Division>();

		foreach (var row in reads)
		{
			var state = row.Saved;
			if (state?.ConfirmedEmployeeId != row.Previous)
				state = new(row.Previous);
			if (!input.SaveAsDraft && row.Previous != row.Next)
			{
				var key = row.Next == null ? AcademicNotificationEventKeys.ClassTeacherRemoved :
					row.Previous == null ? AcademicNotificationEventKeys.ClassTeacherAssigned : AcademicNotificationEventKeys.ClassTeacherReplaced;
				var next = row.Next is Guid current && accounts.TryGetValue(current, out var replacement) ? replacement : null;
				var previous = row.Previous is Guid former && accounts.TryGetValue(former, out var old) && old.UserId != next?.UserId
					? old with { Kind = "PreviousTeacher" } : null;
				state = new(row.Next, key, Guid.NewGuid().ToString("N"), actor, next, previous);
				changes.Add(new(row.Section.Id, state));
			}
			if (state == null || state == row.Saved) continue;
			row.Section.SetProperty(StateProperty, NotificationRuntimeJson.Serialize(state, NotificationErrorCodes.SourceUnavailable));
			updated.Add(row.Section);
		}
		if (updated.Count > 0) await divisions.UpdateManyAsync(updated, autoSave: false, cancellationToken: token);
		return changes;
	}

	internal async Task CaptureAsync(IReadOnlyList<ClassTeacherNoticeChange> changes)
	{
		if (changes.Count == 0 || tenant.Id is not Guid school) return;
		var token = cancellation.Token;
		var events = changes.Select(x => x.State.EventKey!).Distinct().ToArray();
		var enabled = (await queries.ToListAsync((await rules.GetQueryableAsync()).Where(x =>
			x.TenantId == school && x.IsEnabled && !x.IsDeleted && Enumerable.Contains(events, x.EventKey))
			.Select(x => x.EventKey).Distinct(), token)).ToHashSet(StringComparer.Ordinal);
		var occurred = new DateTimeOffset(clock.Now.ToUniversalTime());
		foreach (var change in changes.Where(x => enabled.Contains(x.State.EventKey!)).OrderBy(x => x.DivisionId))
			await writer.WriteAsync(new(change.State.EventKey!, ClassTeacherNotificationSource.Type, change.DivisionId, change.State.Revision!,
				occurred, [], change.State.ActorId), token);
	}
}
