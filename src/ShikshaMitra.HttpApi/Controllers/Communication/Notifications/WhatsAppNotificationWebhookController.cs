using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Auditing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Controllers.Communication.Notifications;

[AllowAnonymous, IgnoreAntiforgeryToken, DisableAuditing, RemoteService(false), UnitOfWork(IsDisabled = true)]
[Route(RoutePath)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class WhatsAppNotificationWebhookController(IWhatsAppWebhookAppService webhook) : AbpControllerBase
{
	public const string RoutePath = "/api/communication/notifications/providers/whatsapp/webhook";
	private const int MaximumBodyBytes = IWhatsAppWebhookAppService.MaximumBodyBytes;

	[HttpGet]
	public IActionResult Verify()
	{
		var challenge = webhook.VerifyChallenge(Request.Query["hub.mode"].ToString(), Request.Query["hub.verify_token"].ToString(),
			Request.Query["hub.challenge"].ToString());
		return challenge == null ? Unauthorized() : Content(challenge, "text/plain");
	}

	[HttpPost, RequestSizeLimit(MaximumBodyBytes + 1)]
	public async Task<IActionResult> ReceiveAsync(CancellationToken cancellationToken)
	{
		if (Request.ContentLength > MaximumBodyBytes) return StatusCode(StatusCodes.Status413PayloadTooLarge);
		var signatures = Request.Headers["X-Hub-Signature-256"];
		if (signatures.Count != 1) return Unauthorized();
		var body = new byte[MaximumBodyBytes + 1];
		var length = await Request.Body.ReadAtLeastAsync(body, body.Length, throwOnEndOfStream: false, cancellationToken);
		if (length > MaximumBodyBytes) return StatusCode(StatusCodes.Status413PayloadTooLarge);
		var result = await webhook.ReceiveAsync(signatures[0], body.AsMemory(0, length), cancellationToken);
		return result switch {
			WhatsAppWebhookReceiptResult.Accepted => Ok(),
			WhatsAppWebhookReceiptResult.InvalidSignature => Unauthorized(),
			_ => BadRequest()
		};
	}
}
