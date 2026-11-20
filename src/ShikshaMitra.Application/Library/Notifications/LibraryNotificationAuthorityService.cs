using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Users;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library.Notifications;

public sealed record LibraryNotificationOrigin(Guid? ActorUserId, string? Action, bool IsSystem = false)
{
	internal static LibraryNotificationOrigin Scheduled => new(null, null, true);
	internal static LibraryNotificationOrigin HoldAllocated => new(null, nameof(LibrarySystemPurpose.HoldAllocator), true);
}

public interface ILibraryNotificationAuthorityService
{
	LibraryNotificationOrigin ForCurrentUser(string action);
	void Capture(LibraryNotificationHistory row, LibraryNotificationOrigin origin);
	Task<bool> CanDispatchAsync(LibraryNotificationHistory row);
	Task<bool> CanReceiveOperationalAsync(Guid userId);
	Task<bool> CanManageSettingsAsync();
}

public class LibraryNotificationAuthorityService(
	IIdentityUserRepository users,
	IRepository<IdentityRole, Guid> roles,
	IUserScopeService scopes,
	ILibraryAccessService access,
	LibraryNotificationRecordScope records,
	IPermissionChecker permissions,
	ICurrentUser currentUser,
	ICurrentTenant tenant,
	ICurrentPrincipalAccessor principals,
	IUnitOfWorkManager units) : ILibraryNotificationAuthorityService, ITransientDependency
{
	private const string ActorKey = "DispatchActorUserId";
	private const string ActionKey = "DispatchAction";
	private const string SystemKey = "DispatchSystemReminder";
	private const string TenantKey = "DispatchTenantId";

	public LibraryNotificationOrigin ForCurrentUser(string action) =>
		new(currentUser.Id ?? throw new AbpAuthorizationException(), action);

	public void Capture(LibraryNotificationHistory row, LibraryNotificationOrigin origin)
	{
		if (row.TenantId != tenant.Id) throw new AbpAuthorizationException();
		if (!row.ExtraProperties.ContainsKey("InitialDispatchActorUserId"))
		{
			row.ExtraProperties["InitialDispatchActorUserId"] = origin.ActorUserId?.ToString("D");
			row.ExtraProperties["InitialDispatchAction"] = origin.Action;
		}
		row.ExtraProperties[ActorKey] = origin.ActorUserId?.ToString("D");
		row.ExtraProperties[ActionKey] = origin.Action;
		row.ExtraProperties[SystemKey] = origin.IsSystem;
		row.ExtraProperties[TenantKey] = tenant.Id?.ToString("D") ?? "host";
	}

	public async Task<bool> CanDispatchAsync(LibraryNotificationHistory row)
	{
		if (row.TenantId != tenant.Id || !row.ExtraProperties.TryGetValue(TenantKey, out var savedTenant) ||
			savedTenant?.ToString() != (tenant.Id?.ToString("D") ?? "host")) return false;
		if (!LibraryNotificationSource.IsConsistent(row.EventType, row.RelatedEntityType, row.RelatedEntityId)) return false;
		if (row.ExtraProperties.TryGetValue(SystemKey, out var system) && bool.TryParse(system?.ToString(), out var isSystem) && isSystem)
		{
			if (row.ExtraProperties.TryGetValue(ActorKey, out var actor) && !string.IsNullOrWhiteSpace(actor?.ToString())) return false;
			var purpose = row.ExtraProperties.GetValueOrDefault(ActionKey)?.ToString();
			return purpose == nameof(LibrarySystemPurpose.HoldAllocator)
				? row.EventType == LibraryNotificationEventTypes.ReservedBookAvailable
				: string.IsNullOrWhiteSpace(purpose) && IsScheduledEvent(row.EventType);
		}
		if (!row.ExtraProperties.TryGetValue(ActorKey, out var actorValue) || !Guid.TryParse(actorValue?.ToString(), out var userId) ||
			!row.ExtraProperties.TryGetValue(ActionKey, out var actionValue)) return false;
		var action = actionValue?.ToString();
		return await CanActAsync(userId, action, row.RecipientMemberId, row);
	}

	public Task<bool> CanManageSettingsAsync() => currentUser.Id.HasValue
		? CanActAsync(currentUser.Id.Value, ShikshaMitraSubmodulePermissions.Library.Notification.Add, null)
		: Task.FromResult(false);

	private async Task<bool> CanActAsync(Guid userId, string? action, Guid? memberId, LibraryNotificationHistory? row = null)
	{
		var required = RequiredPermissions(action);
		if (required == null) return false;
		UserScopeContext scope;
		ClaimsPrincipal principal;
		using (var read = units.Begin(requiresNew: true, isTransactional: false))
		{
			var activePrincipal = await PrincipalAsync(userId);
			if (activePrincipal == null) return false;
			principal = activePrincipal;
			using var actorScope = principals.Change(principal);
			foreach (var permission in required)
				if (!await permissions.IsGrantedAsync(principal, permission)) return false;
			scope = await scopes.GetUserScopeAsync(userId);
			if (scope.UserId != userId || scope.TenantId != tenant.Id || !scope.HasAnyAccess) return false;
			await read.CompleteAsync();
		}
		using var sourceActor = principals.Change(principal);
		using var sourceRead = units.Begin(requiresNew: false, isTransactional: false);
		var allowed = row != null ? await records.CanReadAsync(row, scope) : memberId.HasValue
			? (await access.GetMemberIdsAsync(scope)).Contains(memberId.Value) : scope.HasTenantWideScope();
		await sourceRead.CompleteAsync();
		return allowed;
	}

	public async Task<bool> CanReceiveOperationalAsync(Guid userId)
	{
		using var read = units.Begin(requiresNew: true, isTransactional: false);
		var principal = await PrincipalAsync(userId);
		if (principal == null) return false;
		using var actor = principals.Change(principal);
		if (!await permissions.IsGrantedAsync(principal, ShikshaMitraSubmodulePermissions.Library.Notification.View)) return false;
		var scope = await scopes.GetUserScopeAsync(userId);
		var allowed = scope.UserId == userId && scope.TenantId == tenant.Id && scope.HasTenantWideScope();
		await read.CompleteAsync();
		return allowed;
	}

	private async Task<ClaimsPrincipal?> PrincipalAsync(Guid userId)
	{
		var user = await users.FindAsync(userId, includeDetails: true);
		if (user == null || !user.IsActive || user.TenantId != tenant.Id) return null;
		var roleIds = user.Roles.Select(role => role.RoleId).ToArray();
		var currentRoles = await roles.GetListAsync(role => role.TenantId == tenant.Id && Enumerable.Contains(roleIds, role.Id));
		var claims = new List<Claim> { new(AbpClaimTypes.UserId, user.Id.ToString()), new(AbpClaimTypes.UserName, user.UserName) };
		if (tenant.Id.HasValue) claims.Add(new(AbpClaimTypes.TenantId, tenant.Id.Value.ToString()));
		claims.AddRange(currentRoles.Select(role => new Claim(AbpClaimTypes.Role, role.Name)));
		return new ClaimsPrincipal(new ClaimsIdentity(claims, "LibraryNotification", AbpClaimTypes.UserName, AbpClaimTypes.Role));
	}

	private static string[]? RequiredPermissions(string? action) => action switch
	{
		ShikshaMitraSubmodulePermissions.Library.Notification.Add =>
			[ShikshaMitraSubmodulePermissions.Library.Notification.View, ShikshaMitraSubmodulePermissions.Library.Notification.Add],
		ShikshaMitraSubmodulePermissions.Library.IssueReturn.Issue => [ShikshaMitraSubmodulePermissions.Library.IssueReturn.Issue],
		ShikshaMitraSubmodulePermissions.Library.IssueReturn.Return => [ShikshaMitraSubmodulePermissions.Library.IssueReturn.Return],
		ShikshaMitraSubmodulePermissions.Library.IssueReturn.Reserve =>
			[ShikshaMitraSubmodulePermissions.Library.AllBooks.View, ShikshaMitraSubmodulePermissions.Library.IssueReturn.Reserve],
		_ => null
	};

	private static bool IsScheduledEvent(string eventType) => eventType is
		LibraryNotificationEventTypes.DueDateReminder or LibraryNotificationEventTypes.OverdueReminder or
		LibraryNotificationEventTypes.FineDueReminder or LibraryNotificationEventTypes.ReservedBookAvailable or
		LibraryNotificationEventTypes.ReservationExpiring or LibraryNotificationEventTypes.LowStockAlert;
}
