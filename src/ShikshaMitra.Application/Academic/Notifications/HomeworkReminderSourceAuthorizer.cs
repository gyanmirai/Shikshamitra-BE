using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;
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
	IRepository<HomeworkAssignmentEntity, Guid> tasks, IAsyncQueryableExecuter queries, ICurrentTenant tenant)
	: IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[]
	{
		HomeworkReminderSourceAdapter.EventKey, HomeworkPublicationSourceAdapter.EventKey,
	});

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken cancellationToken)
	{
		if (!EventKeys.Contains(eventKey) || sourceId == Guid.Empty || tenant.Id is not Guid tenantId ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View))
			throw new AbpAuthorizationException();

		var scoped = await access.ApplyCurrentUserScopeAsync((await tasks.GetQueryableAsync()).Where(x =>
			x.TenantId == tenantId && !x.IsDeleted && x.Id == sourceId));
		if (!await queries.AnyAsync(scoped, cancellationToken)) throw new AbpAuthorizationException();
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken cancellationToken) =>
		EnsureCanViewAsync(eventKey, sourceId, cancellationToken);
}
