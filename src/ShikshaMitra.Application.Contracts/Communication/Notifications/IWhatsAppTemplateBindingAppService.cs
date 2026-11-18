using System;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Communication.Notifications;

public sealed record CreateWhatsAppTemplateBindingDto(Guid TemplateId, Guid RevisionId, Guid ProviderSettingId, string ProviderTemplateName);

public sealed record WhatsAppTemplateBindingDto(Guid Id, Guid TemplateId, Guid RevisionId, Guid ProviderSettingId, string ProviderTemplateName,
	string LanguageCode, string? ProviderTemplateId, NotificationProviderTemplateState ProviderStatus, DateTimeOffset? LastSyncedAt);

public interface IWhatsAppTemplateBindingAppService
{
	Task<WhatsAppTemplateBindingDto?> GetAsync(Guid templateId, Guid revisionId, Guid providerSettingId, CancellationToken cancellationToken = default);
	Task<WhatsAppTemplateBindingDto> CreateAsync(CreateWhatsAppTemplateBindingDto input, CancellationToken cancellationToken = default);
	Task<WhatsAppTemplateBindingDto> SubmitAsync(Guid id, CancellationToken cancellationToken = default);
	Task<WhatsAppTemplateBindingDto> SyncAsync(Guid id, CancellationToken cancellationToken = default);
}
