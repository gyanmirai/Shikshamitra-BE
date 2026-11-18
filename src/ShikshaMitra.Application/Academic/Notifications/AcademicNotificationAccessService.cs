using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class AcademicNotificationAccessService(ICurrentTenant tenant, ICurrentUser current,
	IRepository<IdentityUser, Guid> users, IUserScopeAccessService scopes, IPermissionChecker permissions,
	IEnumerable<INotificationSourceAdapter> adapters, IEnumerable<IAcademicNotificationSourceAuthorizer> authorizers,
	AcademicNotificationCatalogue catalogue) : IAcademicNotificationAccessService, ITransientDependency
{
	public Task<AcademicNotificationAccessContext> RequireSendAsync(AcademicNotificationAudienceInput input,
		CancellationToken cancellationToken) => RequireSourceAsync(input.EventKey, input.SourceType, input.SourceId,
		ShikshaMitraSubmodulePermissions.Academics.Notification.Send, cancellationToken);

	internal Task<AcademicNotificationAccessContext> RequireRulePreviewAsync(string eventKey, string sourceType, Guid sourceId,
		CancellationToken cancellationToken) => RequireSourceAsync(eventKey, sourceType, sourceId,
		ShikshaMitraSubmodulePermissions.Academics.Notification.Add, cancellationToken);

	internal async Task<AcademicNotificationAccessContext> RequireManualPreviewAsync(PreviewAcademicNotificationManualDto input,
		CancellationToken cancellationToken)
	{
		if (!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Academics.Notification.View))
			throw new AbpAuthorizationException();
		return await RequireSourceAsync(input.EventKey, input.SourceType, input.SourceId,
			ShikshaMitraSubmodulePermissions.Academics.Notification.Send, cancellationToken);
	}

	private async Task<AcademicNotificationAccessContext> RequireSourceAsync(string eventKey, string sourceType, Guid sourceId,
		string permission, CancellationToken cancellationToken)
	{
		var identity = await RequireIdentityAsync(permission, cancellationToken);
		return await LoadAuthorizedSourceAsync(eventKey, sourceType, sourceId, identity, cancellationToken);
	}

	internal Task<(Guid TenantId, Guid ActorId, UserScopeContext Scope)> RequireManualSenderAsync(CancellationToken cancellationToken) =>
		RequireIdentityAsync(ShikshaMitraSubmodulePermissions.Academics.Notification.Send, cancellationToken);

	internal Task<(Guid TenantId, Guid ActorId, UserScopeContext Scope)> RequireManualApproverAsync(CancellationToken cancellationToken) =>
		RequireIdentityAsync(ShikshaMitraSubmodulePermissions.Academics.Notification.Add, cancellationToken);

	internal Task<AcademicNotificationAccessContext> LoadManualSourceAsync(PreviewAcademicNotificationManualDto input,
		(Guid TenantId, Guid ActorId, UserScopeContext Scope) identity, CancellationToken cancellationToken) =>
		LoadAuthorizedSourceAsync(input.EventKey, input.SourceType, input.SourceId, identity, cancellationToken);

	private Task<(Guid TenantId, Guid ActorId, UserScopeContext Scope)> RequireIdentityAsync(string permission,
		CancellationToken cancellationToken) => AcademicNotificationIdentityAccess.RequireAsync(tenant, current, users,
		scopes, permissions, permission, cancellationToken);

	private async Task<AcademicNotificationAccessContext> LoadAuthorizedSourceAsync(string eventKey, string sourceType, Guid sourceId,
		(Guid TenantId, Guid ActorId, UserScopeContext Scope) identity, CancellationToken cancellationToken)
	{
		var (tenantId, actorId, scope) = identity;
		var definition = catalogue.Get(eventKey);
		if (sourceType != definition.SourceType || sourceId == Guid.Empty)
			throw new BusinessException(NotificationErrorCodes.RecipientIneligible);
		catalogue.EnsureAvailable(eventKey);
		var sources = adapters.Where(x => x.EventKeys.Contains(eventKey)).Take(2).ToArray();
		var guards = authorizers.Where(x => x.EventKeys.Contains(eventKey)).Take(2).ToArray();
		if (sources.Length != 1 || guards.Length != 1) throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
		await guards[0].EnsureCanSendAsync(eventKey, sourceId, cancellationToken);
		var now = DateTimeOffset.UtcNow;
		var snapshot = await sources[0].LoadAsync(new(Guid.Empty, tenantId, eventKey, sourceType,
			sourceId, string.Empty, now, []), cancellationToken);
		if (string.IsNullOrWhiteSpace(snapshot.SourceRevision)) throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
		if (snapshot.ExpiresAt <= now)
			snapshot = snapshot with { IsEligible = false, ExclusionCode = "SourceExpired", EligibleRecipients = [] };
		return new(tenantId, actorId, scope, snapshot);
	}
}
