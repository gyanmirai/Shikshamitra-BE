using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Academic.Timetable.RoomAvailability;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.Security.Claims;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class RoomNotificationReviewer(IConfiguration configuration, RoomAvailabilityAppService bookings,
	IRepository<IdentityUser, Guid> users, AdminPermissionAccess identities, IAsyncQueryableExecuter queries, IPermissionChecker permissions)
	: ITransientDependency
{
	internal async Task<NotificationRecipientRefDto?> FindAsync(RoomReservation booking, Guid requester, CancellationToken token)
	{
		if (booking.TenantId is not Guid tenantId) return null;
		var configured = configuration[$"Notifications:Academic:Tenants:{tenantId:N}:RoomReviewerUserId"];
		if (!Guid.TryParse(configured, out var reviewerId) || reviewerId == Guid.Empty || reviewerId == requester ||
			!await queries.AnyAsync((await users.GetQueryableAsync()).Where(x =>
				x.Id == reviewerId && x.TenantId == tenantId && !x.IsDeleted && x.IsActive), token))
			return null;
		var roles = await identities.GetRolesAsync(reviewerId);
		if (roles == null || !await bookings.CanReceiveReviewNoticeAsync(booking, requester, reviewerId, roles)) return null;
		var claims = new List<Claim>
		{
			new(AbpClaimTypes.UserId, reviewerId.ToString()),
			new(AbpClaimTypes.TenantId, tenantId.ToString())
		};
		claims.AddRange(roles.Select(role => new Claim(AbpClaimTypes.Role, role)));
		var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "RoomNotificationPermissions", AbpClaimTypes.UserName, AbpClaimTypes.Role));
		return await permissions.IsGrantedAsync(principal, ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)
			? new("ConfiguredReviewer", UserId: reviewerId) : null;
	}
}
