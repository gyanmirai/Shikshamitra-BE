using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class HomeworkReminderSourceAuthorizer(HomeworkAppService access, IPermissionChecker permissions,
	IRepository<HomeworkAssignmentEntity, Guid> tasks, IRepository<StudentSubmissionEntity, Guid> submissions,
	IAsyncQueryableExecuter queries, ICurrentTenant tenant)
	: IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[]
	{
		LearningTaskReminderSourceAdapter.HomeworkEventKey, AcademicNotificationEventKeys.HomeworkOverdue,
		AcademicNotificationEventKeys.HomeworkSubmitted,
		HomeworkPublicationSourceAdapter.EventKey, AcademicNotificationEventKeys.HomeworkFeedbackReleased,
		AcademicNotificationEventKeys.HomeworkRevisionRequested
	});

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken cancellationToken)
	{
		if (!EventKeys.Contains(eventKey) || sourceId == Guid.Empty || tenant.Id is not Guid tenantId ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View))
			throw new AbpAuthorizationException();

		var homeworkId = sourceId;
		if (eventKey is AcademicNotificationEventKeys.HomeworkFeedbackReleased or AcademicNotificationEventKeys.HomeworkRevisionRequested)
		{
			var owner = await queries.SingleOrDefaultAsync((await submissions.GetQueryableAsync()).Where(x =>
				x.TenantId == tenantId && x.Id == sourceId && !x.IsDeleted && x.AssignmentId == null)
				.Select(x => x.HomeworkAssignmentId), cancellationToken);
			homeworkId = owner ?? throw new AbpAuthorizationException();
		}
		var scoped = await access.ApplyCurrentUserScopeAsync((await tasks.GetQueryableAsync()).Where(x =>
			x.TenantId == tenantId && !x.IsDeleted && x.Id == homeworkId));
		if (!await queries.AnyAsync(scoped, cancellationToken)) throw new AbpAuthorizationException();
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken cancellationToken) =>
		EnsureCanViewAsync(eventKey, sourceId, cancellationToken);
}
