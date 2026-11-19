using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Users;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record ElectiveSelectionNotice(Guid StudentId, Guid GroupId, Guid SubjectId, string Revision, NotificationSourceScopeDto Scope);

[RemoteService(false), ExposeServices(typeof(ElectiveSelectionNotification), typeof(INotificationSourceAdapter), typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class ElectiveSelectionNotification(IRepository<StudentElectiveSelection, Guid> selections, IRepository<Student, Guid> students,
	ElectiveNotificationAudience audience, AcademicNotificationRuleScopeAccess access, IUserScopeService scopes,
	IPermissionChecker permissions, INotificationPersonalWriteLock locks, IAsyncQueryableExecuter queries,
	ICurrentTenant tenant, ITenantStore tenants, ICurrentUser actor, ICancellationTokenProvider cancellation)
	: INotificationSourceAdapter, IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	internal const string EventKey = AcademicNotificationEventKeys.ElectiveSelectionConfirmed;
	internal const string Type = ElectiveCorrectionNotificationSource.Type;
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { EventKey });

	internal Task LockAsync(Guid divisionId, Guid groupId) => tenant.Id.HasValue
		? locks.AcquireAsync([$"academic-elective-confirmation:{divisionId:N}:{groupId:N}"], cancellation.Token)
		: Task.CompletedTask;

	internal async Task<bool> CanCaptureAsync(Guid mediumId, Guid classId, Guid divisionId)
	{
		if (tenant.Id is not Guid school || actor.Id is not Guid user ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.ClassManagement.StudentElectiveAssignment.Add)) return false;
		var token = cancellation.Token;
		var division = await audience.FindDivisionAsync(school, divisionId, token);
		if (division == null || division.Scope.MediumId != mediumId || division.Scope.ClassId != classId) return false;
		try
		{
			await RequireScopeAsync(school, user, division.Scope, token);
			return true;
		}
		catch (AbpAuthorizationException) { return false; }
	}

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		var school = context.TenantId;
		if (school == Guid.Empty || tenant.Id != school || context.SourceId == Guid.Empty ||
			context.SourceType != Type || context.EventKey != EventKey) return Unavailable();
		var record = await FindAsync(school, context.SourceId, token);
		if (record == null || string.IsNullOrWhiteSpace(record.Revision) ||
			(context.SourceScope != null && record.Scope != context.SourceScope) ||
			(!string.IsNullOrEmpty(context.SourceRevision) && record.Revision != context.SourceRevision)) return Unavailable();

		var scope = record.Scope;
		if (scope.DivisionId is not Guid divisionId ||
			await audience.FindDivisionAsync(school, divisionId, token) is not { } division || division.Scope != scope ||
			!await audience.IsEnrolledAsync(school, record.StudentId, scope, token)) return Unavailable();
		var group = await audience.FindGroupAsync(school, record.GroupId, scope.MediumId, token);
		if (group == null || !group.Subjects.Any(member => member.SubjectId == record.SubjectId)) return Unavailable();
		var published = await audience.FindPublishedAsync(school, record.GroupId, record.SubjectId, scope, token);
		if (published == null) return Unavailable();
		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable();

		var teachers = await audience.FindTeachersAsync(school, published.Value.Assignment, scope, token);
		var recipients = new[] { new NotificationRecipientRefDto("Student", StudentId: record.StudentId) }.Concat(teachers).ToArray();
		var variables = ElectiveNotificationAudience.Variables(schoolName, division.Name, published.Value.Name, group.Name);
		return new(true, null, record.Revision, Link, variables, recipients, AudienceSlices: [new(scope, recipients)]);
	}

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (eventKey != EventKey || tenant.Id is not Guid school || actor.Id is not Guid user || sourceId == Guid.Empty ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.ClassManagement.StudentElectiveAssignment.View) ||
			await FindAsync(school, sourceId, token) is not { } record) throw new AbpAuthorizationException();
		await RequireScopeAsync(school, user, record.Scope, token);
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) =>
		EnsureCanViewAsync(eventKey, sourceId, token);

	private async Task<ElectiveSelectionNotice?> FindAsync(Guid school, Guid id, CancellationToken token)
	{
		var placement = from selection in await selections.GetQueryableAsync()
			join student in await students.GetQueryableAsync() on selection.StudentId equals student.Id
			where selection.Id == id && selection.TenantId == school && !selection.IsDeleted && selection.IsLocked &&
				student.TenantId == school && !student.IsDeleted && student.AcademicMediumId == selection.AcademicMediumId &&
				student.AcademicYearId.HasValue && student.AcademicBoardId.HasValue && student.StandardId.HasValue && student.DivisionId.HasValue
			select new ElectiveSelectionNotice(student.Id, selection.ElectiveGroupId, selection.SelectedSubjectId, selection.ConcurrencyStamp,
				new(student.AcademicYearId, student.AcademicBoardId, selection.AcademicMediumId, student.StandardId, student.DivisionId));
		return await queries.SingleOrDefaultAsync(placement, token);
	}

	private async Task RequireScopeAsync(Guid school, Guid user, NotificationSourceScopeDto scope, CancellationToken token)
	{
		var caller = await scopes.GetUserScopeAsync(user);
		if (caller.TenantId != school) throw new AbpAuthorizationException();
		await access.RequireAsync(new(scope.AcademicYearId, scope.BoardId, scope.MediumId, scope.ClassId, scope.DivisionId), school, caller, token);
	}
}
