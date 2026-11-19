using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Timing;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record SubjectNoticeTeacher(Guid EmployeeId, SubjectTeacherType Role);
internal sealed record SubjectAllocationBefore(Guid ClassId, Guid? ElectiveGroupId, SubjectStatus Status, SubjectNoticeTeacher[] Teachers);
internal sealed record SubjectTeacherNotice(string Revision, SubjectNoticeTeacher[] Current, Guid[] Added, Guid[] Removed);
internal sealed record SubjectWithdrawalNotice(string Revision, SubjectStatus Status, bool IsDeleted, Guid[] Teachers);
internal sealed record SubjectNoticeState(Guid ClassId, Guid? ElectiveGroupId, string? Publication = null,
	SubjectTeacherNotice? Change = null, SubjectWithdrawalNotice? Withdrawal = null);
internal sealed record SubjectNoticeCapture(Guid AssignmentId, string EventKey, string Revision);

[RemoteService(false)]
public sealed class SubjectAllocationNotification(IRepository<NotificationRule, Guid> rules, IAsyncQueryableExecuter queries,
	INotificationIntentWriter writer, ICurrentTenant tenant, ICancellationTokenProvider cancellation, IClock clock,
	IPermissionChecker permissions)
	: ITransientDependency
{
	private const string StateProperty = "AcademicSubjectAllocationNotice";

	internal Task<bool> CanRecordAsync(bool deletion = false) => permissions.IsGrantedAsync(deletion
		? ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SubjectAssignment.Delete
		: ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SubjectAssignment.Add);

	internal static SubjectNoticeState? Read(AssignedSubject assignment) =>
		assignment.GetProperty<string>(StateProperty) is { } json
			? NotificationRuntimeJson.Deserialize<SubjectNoticeState>(json, NotificationErrorCodes.SourceUnavailable)
			: null;

	internal static SubjectNoticeTeacher[] Teachers(AssignedSubject assignment) => assignment.Teachers
		.Where(x => x.TenantId == assignment.TenantId && x.TeacherId != Guid.Empty)
		.Select(x => new SubjectNoticeTeacher(x.TeacherId, x.TeacherType)).Distinct().OrderBy(x => x.EmployeeId).ThenBy(x => x.Role).ToArray();

	internal static Guid? GroupId(AssignedSubject assignment) =>
		assignment.ElectiveGroupId is Guid id && id != Guid.Empty ? id : null;

	internal static SubjectAllocationBefore Before(AssignedSubject assignment) =>
		new(assignment.StandardId, GroupId(assignment), assignment.Status, Teachers(assignment));

	internal static void Record(List<SubjectNoticeCapture> messages, AssignedSubject assignment, SubjectAllocationBefore? before,
		bool deleting = false, bool capture = true)
	{
		if (!assignment.TenantId.HasValue) return;
		var group = GroupId(assignment);
		var samePlacement = before == null || (before.ClassId == assignment.StandardId && before.ElectiveGroupId == group);
		if (!capture && !deleting && samePlacement && before?.Status == assignment.Status && before.Teachers.SequenceEqual(Teachers(assignment))) return;
		var saved = Read(assignment);
		var next = saved;
		string? eventKey = null;
		var revision = Guid.NewGuid().ToString("N");
		if (!capture || !samePlacement)
			next = null;
		else if (before?.Status == SubjectStatus.Published && (deleting || assignment.Status != SubjectStatus.Published))
		{
			var teachers = before.Teachers.Select(x => x.EmployeeId).Distinct().ToArray();
			next = new(assignment.StandardId, group, Withdrawal: new(revision, assignment.Status, deleting, teachers));
			eventKey = AcademicNotificationEventKeys.SubjectWithdrawn;
		}
		else if (!deleting && assignment.Status == SubjectStatus.Published)
		{
			if (before?.Status != SubjectStatus.Published)
			{
				next = new(assignment.StandardId, group, Publication: revision);
				eventKey = AcademicNotificationEventKeys.SubjectAllocated;
			}
			else
			{
				var current = Teachers(assignment);
				if (!before.Teachers.SequenceEqual(current))
				{
					var added = current.Except(before.Teachers).Select(x => x.EmployeeId).Distinct().ToArray();
					var removed = before.Teachers.Select(x => x.EmployeeId).Except(current.Select(x => x.EmployeeId)).ToArray();
					next = (saved ?? new(assignment.StandardId, group)) with { Change = new(revision, current, added, removed) };
					eventKey = AcademicNotificationEventKeys.SubjectTeacherChanged;
				}
			}
		}
		else if (deleting || (before != null && before.Status != assignment.Status))
			next = null;

		if (next != saved)
		{
			if (next == null) assignment.RemoveProperty(StateProperty);
			else assignment.SetProperty(StateProperty, NotificationRuntimeJson.Serialize(next, NotificationErrorCodes.SourceUnavailable));
		}
		if (eventKey != null) messages.Add(new(assignment.Id, eventKey, revision));
	}

	internal async Task CaptureAsync(IReadOnlyList<SubjectNoticeCapture> messages, Guid? actor)
	{
		if (messages.Count == 0 || tenant.Id is not Guid school) return;
		var token = cancellation.Token;
		var events = messages.Select(x => x.EventKey).Distinct().ToArray();
		var enabled = (await queries.ToListAsync((await rules.GetQueryableAsync()).Where(x =>
			x.TenantId == school && x.IsEnabled && !x.IsDeleted && Enumerable.Contains(events, x.EventKey))
			.Select(x => x.EventKey).Distinct(), token)).ToHashSet(StringComparer.Ordinal);
		var now = new DateTimeOffset(clock.Now.ToUniversalTime());
		foreach (var message in messages.Where(x => enabled.Contains(x.EventKey)).OrderBy(x => x.AssignmentId))
			await writer.WriteAsync(new(message.EventKey, SubjectAllocationNotificationSource.Type, message.AssignmentId, message.Revision,
				now, [], actor), token);
	}
}
