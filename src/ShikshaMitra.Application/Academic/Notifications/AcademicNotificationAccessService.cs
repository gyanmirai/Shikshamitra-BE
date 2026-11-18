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
	public async Task<AcademicNotificationAccessContext> RequireSendAsync(AcademicNotificationAudienceInput input,
		CancellationToken cancellationToken)
	{
		var (tenantId, actorId, scope) = await AcademicNotificationIdentityAccess.RequireAsync(tenant, current, users,
			scopes, permissions, ShikshaMitraSubmodulePermissions.Academics.Notification.Send, cancellationToken);
		var definition = catalogue.Get(input.EventKey);
		if (input.SourceType != definition.SourceType || input.SourceId == Guid.Empty)
			throw new BusinessException(NotificationErrorCodes.RecipientIneligible);
		catalogue.EnsureAvailable(input.EventKey);
		var sources = adapters.Where(x => x.EventKeys.Contains(input.EventKey)).ToArray();
		var guards = authorizers.Where(x => x.EventKeys.Contains(input.EventKey)).ToArray();
		if (sources.Length != 1 || guards.Length != 1) throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
		await guards[0].EnsureCanSendAsync(input.EventKey, input.SourceId, cancellationToken);
		var snapshot = await sources[0].LoadAsync(new(Guid.Empty, tenantId, input.EventKey, input.SourceType,
			input.SourceId, string.Empty, DateTimeOffset.UtcNow, []), cancellationToken);
		if (string.IsNullOrWhiteSpace(snapshot.SourceRevision)) throw new BusinessException(NotificationErrorCodes.SourceUnavailable);
		if (snapshot.ExpiresAt <= DateTimeOffset.UtcNow)
			snapshot = snapshot with { IsEligible = false, ExclusionCode = "SourceExpired", EligibleRecipients = [] };
		return new(tenantId, actorId, scope, snapshot);
	}
}
