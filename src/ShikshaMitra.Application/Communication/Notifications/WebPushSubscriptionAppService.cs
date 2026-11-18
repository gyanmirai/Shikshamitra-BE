using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.Validation;

namespace ShikshaMitra.Communication.Notifications;

[Authorize, RemoteService(false), DisableAuditing]
public class WebPushSubscriptionAppService(NotificationSelfIdentity identity, WebPushEndpointValidator endpoints,
	WebPushKeyConfiguration configuration, WebPushSubscriptionProtector protector,
	IRepository<WebPushSubscription, Guid> subscriptions, INotificationPersonalWriteLock writeLock, IDataFilter filters)
	: ApplicationService, IWebPushSubscriptionAppService
{
	public virtual async Task<WebPushPublicKeyDto> GetPublicKeyAsync(CancellationToken cancellationToken = default)
	{
		await identity.RequireAsync(cancellationToken);
		var keys = configuration.Read();
		return keys == null ? new(false, null, null, NotificationErrorCodes.ProviderUnavailable) : new(true, keys.PublicKey, keys.Version);
	}

	[UnitOfWork(true)]
	public virtual async Task<WebPushSubscriptionDto> RegisterAsync(RegisterWebPushSubscriptionDto input, CancellationToken cancellationToken = default)
	{
		var owner = await identity.RequireAsync(cancellationToken);
		var validation = await new WebPushSubscriptionValidator().ValidateAsync(input, cancellationToken);
		if (!validation.IsValid) throw new AbpValidationException("Invalid push subscription.",
			validation.Errors.Select(x => new ValidationResult(x.ErrorMessage, [x.PropertyName])).ToList());
		var keys = configuration.Read() ?? throw new BusinessException(NotificationErrorCodes.ProviderUnavailable);
		if (input.VapidKeyVersion != keys.Version) throw new BusinessException(NotificationErrorCodes.PushKeyChanged);
		var endpoint = await endpoints.ValidateAsync(input.Endpoint, cancellationToken);
		input = input with { Endpoint = endpoint.Uri.AbsoluteUri };
		var hash = WebPushSubscriptionProtector.HashEndpoint(input.Endpoint);
		await writeLock.AcquireAsync(["push-subscriptions"], cancellationToken);
		WebPushSubscription? existing;
		using (filters.Disable<ISoftDelete>())
			existing = await AsyncExecuter.FirstOrDefaultAsync((await subscriptions.GetQueryableAsync())
				.Where(x => x.TenantId == owner.TenantId && x.EndpointHash == hash), cancellationToken);
		if (existing?.IsDeleted == true) throw new BusinessException(NotificationErrorCodes.PushSubscriptionInvalid);
		if (existing != null && existing.UserId != owner.UserId)
		{
			var prior = protector.Unprotect(existing);
			if (existing.BrowserInstallationId != input.BrowserInstallationId || !SameKey(prior.Keys.Auth, input.Keys.Auth) ||
				!SameKey(prior.Keys.P256dh, input.Keys.P256dh)) throw new AbpAuthorizationException();
		}
		var active = await AsyncExecuter.ToListAsync((await subscriptions.GetQueryableAsync()).Where(x => x.TenantId == owner.TenantId &&
			x.BrowserInstallationId == input.BrowserInstallationId && x.IsEnabled && !x.IsDeleted && x.Id != (existing == null ? Guid.Empty : existing.Id)), cancellationToken);
		if (active.Any(x => x.UserId != owner.UserId)) throw new AbpDbConcurrencyException();
		foreach (var old in active) { old.Disable(); await subscriptions.UpdateAsync(old, autoSave: true, cancellationToken: cancellationToken); }
		var protectedValue = protector.Protect(owner, input);
		if (existing == null)
		{
			existing = new(GuidGenerator.Create(), owner.TenantId, owner.UserId, input.BrowserInstallationId, hash, protectedValue, keys.Version, DateTimeOffset.UtcNow);
			await subscriptions.InsertAsync(existing, autoSave: true, cancellationToken: cancellationToken);
		}
		else
		{
			existing.Rebind(owner.UserId, input.BrowserInstallationId, protectedValue, keys.Version, DateTimeOffset.UtcNow);
			await subscriptions.UpdateAsync(existing, autoSave: true, cancellationToken: cancellationToken);
		}
		return new(existing.Id, existing.BrowserInstallationId, existing.IsEnabled, existing.VapidKeyVersion, existing.LastActivityAt);
	}

	[UnitOfWork(true)]
	public virtual async Task DisableAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var owner = await identity.RequireAsync(cancellationToken);
		await writeLock.AcquireAsync(["push-subscriptions"], cancellationToken);
		var row = await AsyncExecuter.FirstOrDefaultAsync((await subscriptions.GetQueryableAsync()).Where(x => x.Id == id &&
			x.TenantId == owner.TenantId && x.UserId == owner.UserId && !x.IsDeleted), cancellationToken)
			?? throw new EntityNotFoundException(typeof(WebPushSubscription), id);
		row.Disable(); await subscriptions.UpdateAsync(row, autoSave: true, cancellationToken: cancellationToken);
	}

	private static bool SameKey(string first, string second) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(first), Encoding.UTF8.GetBytes(second));
}
