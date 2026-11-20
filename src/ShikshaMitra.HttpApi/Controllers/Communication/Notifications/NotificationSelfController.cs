using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Auditing;

namespace ShikshaMitra.Controllers.Communication.Notifications;

[Authorize, DisableAuditing, Area("app"), RemoteService(Name = "ShikshaMitra")]
[Route("api/communication/notifications/me")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class NotificationSelfController(INotificationInboxAppService inbox, INotificationPreferenceAppService preferences,
	IWebPushSubscriptionAppService push) : AbpControllerBase
{
	[HttpGet("inbox")]
	public Task<PagedResultDto<NotificationInboxItemDto>> GetInboxAsync([FromQuery] GetNotificationInboxInput input, CancellationToken cancellationToken) =>
		inbox.GetListAsync(input, cancellationToken);

	[HttpGet("unread-count")]
	public Task<NotificationUnreadCountDto> GetUnreadCountAsync(CancellationToken cancellationToken) => inbox.GetUnreadCountAsync(cancellationToken);

	[HttpPost("inbox/{id:guid}/read")]
	public Task<NotificationInboxItemDto> MarkReadAsync(Guid id, CancellationToken cancellationToken) => inbox.MarkReadAsync(id, cancellationToken);

	[HttpPost("inbox/{id:guid}/acknowledge")]
	public Task<NotificationInboxItemDto> AcknowledgeAsync(Guid id, CancellationToken cancellationToken) => inbox.AcknowledgeAsync(id, cancellationToken);

	[HttpGet("preferences")]
	public Task<IReadOnlyList<NotificationPreferenceDto>> GetPreferencesAsync(CancellationToken cancellationToken) => preferences.GetListAsync(cancellationToken);

	[HttpGet("preferences/options")]
	public Task<NotificationPreferenceOptionsDto> GetPreferenceOptionsAsync(CancellationToken cancellationToken) =>
		preferences.GetOptionsAsync(cancellationToken);

	[HttpPut("preferences")]
	public Task<NotificationPreferenceDto> SavePreferencesAsync([FromBody] SaveNotificationPreferenceDto input, CancellationToken cancellationToken) =>
		preferences.SaveAsync(input, cancellationToken);

	[HttpGet("push/public-key")]
	public Task<WebPushPublicKeyDto> GetPushPublicKeyAsync(CancellationToken cancellationToken) => push.GetPublicKeyAsync(cancellationToken);

	[HttpPost("push/subscriptions")]
	public Task<WebPushSubscriptionDto> RegisterPushAsync([FromBody] RegisterWebPushSubscriptionDto input, CancellationToken cancellationToken) =>
		push.RegisterAsync(input, cancellationToken);

	[HttpDelete("push/subscriptions/{id:guid}")]
	public Task DisablePushAsync(Guid id, CancellationToken cancellationToken) => push.DisableAsync(id, cancellationToken);
}
