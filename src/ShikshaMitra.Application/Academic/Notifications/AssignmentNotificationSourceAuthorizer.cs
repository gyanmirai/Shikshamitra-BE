using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(IAcademicNotificationSourceAuthorizer), typeof(AssignmentNotificationSourceAuthorizer))]
public sealed class AssignmentNotificationSourceAuthorizer(IRepository<AssignmentEntity, Guid> assignments,
	IRepository<StudentSubmissionEntity, Guid> submissions, IUserScopeService scopes, IPermissionChecker permissions,
	IAsyncQueryableExecuter queries, ICurrentTenant tenant, ICurrentUser user)
	: IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[]
	{
		LearningTaskReminderSourceAdapter.AssignmentEventKey, AcademicNotificationEventKeys.AssignmentOverdue,
		AssignmentPublicationSourceAdapter.EventKey,
		LearningTaskFeedbackReleaseSourceAdapter.AssignmentEventKey
	});

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken cancellationToken)
	{
		if (!EventKeys.Contains(eventKey) || sourceId == Guid.Empty || tenant.Id is not Guid tenantId || user.Id is not Guid userId)
			throw new AbpAuthorizationException();

		var publication = eventKey == AssignmentPublicationSourceAdapter.EventKey;
		var viewPermission = publication
			? ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View
			: ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.View;
		if (!await permissions.IsGrantedAsync(viewPermission))
			throw new AbpAuthorizationException();

		if (eventKey == LearningTaskFeedbackReleaseSourceAdapter.AssignmentEventKey)
		{
			var submission = await queries.SingleOrDefaultAsync((await submissions.GetQueryableAsync()).Where(x =>
				x.TenantId == tenantId && x.Id == sourceId && !x.IsDeleted && !x.HomeworkAssignmentId.HasValue)
				.Select(x => new { x.AssignmentId, x.DivisionId }), cancellationToken);
			if (submission?.AssignmentId is not Guid assignmentId ||
				!await CanAccessFeedbackDivisionAsync(tenantId, assignmentId, submission.DivisionId, userId, cancellationToken))
				throw new AbpAuthorizationException();
			return;
		}

		var scope = await scopes.GetUserScopeAsync(userId);
		var sources = (await assignments.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted);
		sources = publication
			? sources.Where(x => x.DivisionSchedules.Any(s => s.Id == sourceId && s.TenantId == tenantId && s.IsPublished && s.PublishedAt.HasValue))
			: sources.Where(x => x.Id == sourceId);
		if (!await queries.AnyAsync(AssignmentV2AppService.ApplyAssignmentScope(sources, scope), cancellationToken))
			throw new AbpAuthorizationException();
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken cancellationToken) =>
		EnsureCanViewAsync(eventKey, sourceId, cancellationToken);

	internal async Task<bool> CanAccessFeedbackDivisionAsync(Guid tenantId, Guid assignmentId, Guid divisionId, Guid userId,
		CancellationToken cancellationToken)
	{
		var assignment = await queries.SingleOrDefaultAsync((await assignments.WithDetailsAsync(x => x.DivisionSchedules))
			.Where(x => x.TenantId == tenantId && !x.IsDeleted && x.Id == assignmentId &&
				x.DivisionSchedules.Any(s => s.TenantId == tenantId && s.DivisionId == divisionId)), cancellationToken);
		return assignment != null && AssignmentScopePolicy.CanAccessDivision(await scopes.GetUserScopeAsync(userId), assignment, divisionId);
	}
}
