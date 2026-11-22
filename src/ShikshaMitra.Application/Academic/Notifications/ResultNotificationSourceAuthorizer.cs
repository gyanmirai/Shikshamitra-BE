using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class ResultNotificationSourceAuthorizer(ResultNotificationSource source, ExamResultAppService results,
	IPermissionChecker permissions, ICurrentTenant tenant) : IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	public IReadOnlyCollection<string> EventKeys => source.EventKeys;

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (!EventKeys.Contains(eventKey) || sourceId == Guid.Empty || tenant.Id is not Guid tenantId ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.View))
			throw new AbpAuthorizationException();
		var record = await source.FindAsync(tenantId, sourceId, token);
		if (record == null) throw new AbpAuthorizationException();
		try { _ = await results.GetScopedExamAsync(record.Header.ExamId, record.Header.DivisionId); }
		catch (BusinessException) { throw new AbpAuthorizationException(); }
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) => EnsureCanViewAsync(eventKey, sourceId, token);
}
