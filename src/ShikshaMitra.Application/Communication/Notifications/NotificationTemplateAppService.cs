using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;

namespace ShikshaMitra.Communication.Notifications;

[Authorize, RemoteService(false), DisableAuditing]
public class NotificationTemplateAppService(NotificationTemplateRevisionService revisions, NotificationTemplatePreviewService previews)
	: ApplicationService, INotificationTemplateAppService
{
	public virtual Task<int> InitializeDefaultsAsync(string eventKey, CancellationToken cancellationToken = default) => revisions.InitializeDefaultsAsync(eventKey, cancellationToken);
	public virtual Task<NotificationTemplateDto> GetAsync(Guid templateId, CancellationToken cancellationToken = default) => revisions.GetAsync(templateId, cancellationToken);
	public virtual Task<IReadOnlyList<NotificationTemplateDto>> GetHistoryAsync(Guid templateId, CancellationToken cancellationToken = default) => revisions.GetHistoryAsync(templateId, cancellationToken);
	public virtual Task<PagedResultDto<NotificationTemplateDto>> GetListAsync(GetNotificationTemplateListInput input, CancellationToken cancellationToken = default) => revisions.GetListAsync(input, cancellationToken);
	public virtual Task<NotificationTemplateDto> CreateAsync(CreateNotificationTemplateDto input, CancellationToken cancellationToken = default) => revisions.CreateAsync(input, cancellationToken);
	public virtual Task<NotificationTemplateDto> UpdateDraftAsync(Guid templateId, UpdateNotificationTemplateDraftDto input, CancellationToken cancellationToken = default) => revisions.UpdateDraftAsync(templateId, input, cancellationToken);
	public virtual Task<NotificationTemplateDto> PublishAsync(Guid templateId, NotificationTemplateRevisionCommandDto input, CancellationToken cancellationToken = default) => revisions.PublishAsync(templateId, input, cancellationToken);
	public virtual Task<NotificationTemplateDto> RestoreDefaultAsync(Guid templateId, RestoreNotificationTemplateDto input, CancellationToken cancellationToken = default) => revisions.RestoreDefaultAsync(templateId, input.HeaderConcurrencyStamp, cancellationToken);
	public virtual Task RetireAsync(Guid templateId, NotificationTemplateRevisionCommandDto input, CancellationToken cancellationToken = default) => revisions.RetireAsync(templateId, input, cancellationToken);
	public virtual Task DeleteDraftAsync(Guid templateId, NotificationTemplateRevisionCommandDto input, CancellationToken cancellationToken = default) => revisions.DeleteDraftAsync(templateId, input, cancellationToken);
	public virtual Task<NotificationTemplatePreviewDto> PreviewAsync(Guid templateId, Guid revisionId, CancellationToken cancellationToken = default) => previews.PreviewAsync(templateId, revisionId, cancellationToken);
}
