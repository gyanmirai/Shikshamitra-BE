using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Auditing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Controllers.Communication.Notifications;

[Authorize, DisableAuditing, Area("app"), RemoteService(Name = "ShikshaMitra"), UnitOfWork(IsDisabled = true)]
[Route("api/communication/notifications/providers/whatsapp/templates")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class WhatsAppTemplateBindingsController(IWhatsAppTemplateBindingAppService bindings) : AbpControllerBase
{
	[HttpGet("context")]
	public Task<WhatsAppTemplateBindingContextDto> GetContextAsync(Guid templateId, Guid revisionId, CancellationToken cancellationToken) =>
		bindings.GetContextAsync(templateId, revisionId, cancellationToken);

	[HttpGet]
	public Task<WhatsAppTemplateBindingDto?> GetAsync(Guid templateId, Guid revisionId, Guid providerSettingId, CancellationToken cancellationToken) =>
		bindings.GetAsync(templateId, revisionId, providerSettingId, cancellationToken);

	[HttpPost]
	public Task<WhatsAppTemplateBindingDto> CreateAsync([FromBody] CreateWhatsAppTemplateBindingDto input, CancellationToken cancellationToken) =>
		bindings.CreateAsync(input, cancellationToken);

	[HttpPost("{id:guid}/submit")]
	public Task<WhatsAppTemplateBindingDto> SubmitAsync(Guid id, CancellationToken cancellationToken) => bindings.SubmitAsync(id, cancellationToken);

	[HttpPost("{id:guid}/sync")]
	public Task<WhatsAppTemplateBindingDto> SyncAsync(Guid id, CancellationToken cancellationToken) => bindings.SyncAsync(id, cancellationToken);
}
