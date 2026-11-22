using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Communication.Notifications;

public interface INotificationTemplateReadService
{
	Task<NotificationTemplateDto> GetAsync(Guid templateId, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<NotificationTemplateDto>> GetHistoryAsync(Guid templateId, CancellationToken cancellationToken = default);
	Task<PagedResultDto<NotificationTemplateDto>> GetListAsync(GetNotificationTemplateListInput input, CancellationToken cancellationToken = default);
}

public interface INotificationTemplateAppService : IApplicationService, INotificationTemplateReadService
{
	Task<PagedResultDto<PublishedNotificationTemplateOptionDto>> GetPublishedOptionsAsync(GetNotificationTemplateListInput input,
		CancellationToken cancellationToken = default);
	Task<int> InitializeDefaultsAsync(string eventKey, CancellationToken cancellationToken = default);
	Task<NotificationTemplateDto> CreateAsync(CreateNotificationTemplateDto input, CancellationToken cancellationToken = default);
	Task<NotificationTemplateDto> UpdateDraftAsync(Guid templateId, UpdateNotificationTemplateDraftDto input, CancellationToken cancellationToken = default);
	Task<NotificationTemplateDto> PublishAsync(Guid templateId, NotificationTemplateRevisionCommandDto input, CancellationToken cancellationToken = default);
	Task<NotificationTemplateDto> RestoreDefaultAsync(Guid templateId, RestoreNotificationTemplateDto input, CancellationToken cancellationToken = default);
	Task RetireAsync(Guid templateId, NotificationTemplateRevisionCommandDto input, CancellationToken cancellationToken = default);
	Task DeleteDraftAsync(Guid templateId, NotificationTemplateRevisionCommandDto input, CancellationToken cancellationToken = default);
	Task<NotificationTemplatePreviewDto> PreviewAsync(Guid templateId, Guid revisionId, CancellationToken cancellationToken = default);
	Task<NotificationTemplatePreviewDto> PreviewDraftAsync(PreviewNotificationTemplateDraftDto input, CancellationToken cancellationToken = default);
}
