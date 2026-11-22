using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable.RoomAvailability;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class RoomNotificationSourceAuthorizer(RoomAvailabilityAppService bookings, IPermissionChecker permissions,
	ICurrentTenant tenant) : IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	public IReadOnlyCollection<string> EventKeys => RoomNotificationSource.SupportedEvents;

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (!EventKeys.Contains(eventKey) || sourceId == Guid.Empty || tenant.Id is not Guid tenantId ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View) ||
			!await bookings.CanViewNotificationSourceAsync(tenantId, sourceId, token))
			throw new AbpAuthorizationException();
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) =>
		EnsureCanViewAsync(eventKey, sourceId, token);
}
