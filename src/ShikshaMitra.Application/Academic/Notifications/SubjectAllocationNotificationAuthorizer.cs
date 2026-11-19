using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class SubjectAllocationNotificationAuthorizer(SubjectAllocationNotificationSource source, SubjectAssignmentAppService subjects,
	IPermissionChecker permissions, ICurrentTenant tenant) : IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	public IReadOnlyCollection<string> EventKeys => source.EventKeys;

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (!EventKeys.Contains(eventKey) || sourceId == Guid.Empty || tenant.Id is not Guid school ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SubjectAssignment.View))
			throw new AbpAuthorizationException();
		var record = await source.FindAsync(school, sourceId, eventKey == AcademicNotificationEventKeys.SubjectWithdrawn, token)
			?? throw new AbpAuthorizationException();
		try { await subjects.EnsureCurrentUserCanAccessAssignmentAsync(record.Assignment, "subject-assignment-read"); }
		catch (BusinessException) { throw new AbpAuthorizationException(); }
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) =>
		EnsureCanViewAsync(eventKey, sourceId, token);
}
