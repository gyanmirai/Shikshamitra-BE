using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Auditing;

namespace ShikshaMitra.Controllers.Academic.Notifications;

[Authorize, DisableAuditing, Area("app"), RemoteService(Name = "ShikshaMitra")]
[Route("api/academic/notifications/templates")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AcademicNotificationTemplateController(INotificationTemplateAppService templates,
	IAcademicNotificationTemplateTestSendAppService tests) : AbpControllerBase
{
	private const string View = ShikshaMitraSubmodulePermissions.Academics.Notification.View;
	private const string Add = ShikshaMitraSubmodulePermissions.Academics.Notification.Add;
	private const string Delete = ShikshaMitraSubmodulePermissions.Academics.Notification.Delete;

	[HttpGet, Authorize(View)]
	public Task<PagedResultDto<NotificationTemplateDto>> GetListAsync([FromQuery] GetNotificationTemplateListInput input,
		CancellationToken cancellationToken) => templates.GetListAsync(input, cancellationToken);

	[HttpGet("options"), Authorize(View)]
	public Task<PagedResultDto<PublishedNotificationTemplateOptionDto>> GetPublishedOptionsAsync(
		[FromQuery] GetNotificationTemplateListInput input, CancellationToken cancellationToken) =>
		templates.GetPublishedOptionsAsync(input, cancellationToken);

	[HttpGet("{id:guid}"), Authorize(View)]
	public Task<NotificationTemplateDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
		templates.GetAsync(id, cancellationToken);

	[HttpGet("{id:guid}/history"), Authorize(View)]
	public Task<IReadOnlyList<NotificationTemplateDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken) =>
		templates.GetHistoryAsync(id, cancellationToken);

	[HttpGet("{id:guid}/preview/{revisionId:guid}"), Authorize(View)]
	public Task<NotificationTemplatePreviewDto> PreviewAsync(Guid id, Guid revisionId, CancellationToken cancellationToken) =>
		templates.PreviewAsync(id, revisionId, cancellationToken);

	[HttpPost, Authorize(Add)]
	public Task<NotificationTemplateDto> CreateAsync([FromBody] CreateNotificationTemplateDto input,
		CancellationToken cancellationToken) => templates.CreateAsync(input, cancellationToken);

	[HttpPost("initialize-defaults"), Authorize(Add)]
	public Task<int> InitializeDefaultsAsync([FromBody] InitializeAcademicNotificationDefaultsDto input,
		CancellationToken cancellationToken) => templates.InitializeDefaultsAsync(input.EventKey, cancellationToken);

	[HttpPut("{id:guid}/draft"), Authorize(Add)]
	public Task<NotificationTemplateDto> UpdateDraftAsync(Guid id, [FromBody] UpdateNotificationTemplateDraftDto input,
		CancellationToken cancellationToken) => templates.UpdateDraftAsync(id, input, cancellationToken);

	[HttpPost("{id:guid}/publish"), Authorize(Add)]
	public Task<NotificationTemplateDto> PublishAsync(Guid id, [FromBody] NotificationTemplateRevisionCommandDto input,
		CancellationToken cancellationToken) => templates.PublishAsync(id, input, cancellationToken);

	[HttpPost("{id:guid}/test-send"), Authorize(Add)]
	public async Task<ActionResult<AcademicNotificationTemplateTestSendDto>> TestSendAsync(Guid id,
		[FromBody] AcademicNotificationTemplateTestSendInput input, CancellationToken cancellationToken) =>
		Accepted(await tests.SendAsync(id, input, cancellationToken));

	[HttpPost("{id:guid}/restore-default"), Authorize(Add)]
	public Task<NotificationTemplateDto> RestoreDefaultAsync(Guid id, [FromBody] RestoreNotificationTemplateDto input,
		CancellationToken cancellationToken) => templates.RestoreDefaultAsync(id, input, cancellationToken);

	[HttpDelete("{id:guid}/draft"), Authorize(Delete)]
	public Task DeleteDraftAsync(Guid id, [FromBody] NotificationTemplateRevisionCommandDto input,
		CancellationToken cancellationToken) => templates.DeleteDraftAsync(id, input, cancellationToken);
}
