using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class SubstitutionNotificationSourceAuthorizer(SubstitutionAppService duties,
	IPermissionChecker permissions, ICurrentTenant tenant) : IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	public IReadOnlyCollection<string> EventKeys => SubstitutionNotificationSource.SupportedEvents;

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id == null || sourceId == Guid.Empty || !EventKeys.Contains(eventKey) ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View))
			throw new AbpAuthorizationException();
		try { await duties.EnsureCanViewNotificationSourceAsync(sourceId, cancellationToken); }
		catch (EntityNotFoundException) { throw new AbpAuthorizationException(); }
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken cancellationToken) =>
		EnsureCanViewAsync(eventKey, sourceId, cancellationToken);
}
