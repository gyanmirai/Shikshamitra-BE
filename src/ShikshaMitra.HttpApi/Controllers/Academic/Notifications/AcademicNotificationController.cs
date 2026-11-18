using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Auditing;

namespace ShikshaMitra.Controllers.Academic.Notifications;

[Authorize, DisableAuditing, Area("app"), RemoteService(Name = "ShikshaMitra")]
[Route("api/academic/notifications")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AcademicNotificationController(IAcademicNotificationCatalogueAppService catalogue, IAcademicNotificationRuleAppService rules,
	IAcademicNotificationManualAppService manual, IAcademicNotificationHistoryAppService history) : AbpControllerBase
{
	[HttpGet("catalogue")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Notification.View)]
	public Task<AcademicNotificationCatalogueDto> GetCatalogueAsync(CancellationToken cancellationToken) =>
		catalogue.GetAsync(cancellationToken);

	[HttpGet("rules")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Notification.View)]
	public Task<PagedResultDto<AcademicNotificationRuleListItemDto>> GetRulesAsync([FromQuery] GetAcademicNotificationRulesDto input,
		CancellationToken cancellationToken) => rules.GetListAsync(input, cancellationToken);

	[HttpPut("rules/{eventKey}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Notification.Add)]
	public Task<AcademicNotificationRuleDto> SaveRuleAsync(string eventKey, [FromBody] SaveAcademicNotificationRuleDto input,
		CancellationToken cancellationToken) => rules.SaveAsync(eventKey, input, cancellationToken);

	[HttpPost("rules/{eventKey}/preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Notification.Add)]
	public Task<AcademicNotificationRulePreviewDto> PreviewRuleAsync(string eventKey, [FromBody] PreviewAcademicNotificationRuleDto input,
		CancellationToken cancellationToken) => rules.PreviewAsync(eventKey, input, cancellationToken);

	[HttpPost("manual/preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Notification.View)]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Notification.Send)]
	public Task<AcademicNotificationManualPreviewDto> PreviewManualAsync([FromBody] PreviewAcademicNotificationManualDto input,
		CancellationToken cancellationToken) => manual.PreviewAsync(input, cancellationToken);

	[HttpPost("manual/send")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Notification.Send)]
	public async Task<ActionResult<AcademicNotificationOperationDto>> SendManualAsync(
		[FromBody] ConfirmAcademicNotificationManualDto input, CancellationToken cancellationToken) =>
		Accepted(await manual.SendAsync(input, cancellationToken));

	[HttpPost("operations/{id:guid}/approve")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Notification.Add)]
	public Task<AcademicNotificationOperationDto> ApproveManualAsync(Guid id, CancellationToken cancellationToken) =>
		manual.ApproveAsync(id, cancellationToken);

	[HttpPost("operations/{id:guid}/reject")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Notification.Add)]
	public Task<AcademicNotificationOperationDto> RejectManualAsync(Guid id, CancellationToken cancellationToken) =>
		manual.RejectAsync(id, cancellationToken);

	[HttpGet("operations/{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Notification.View)]
	public Task<AcademicNotificationOperationStatusDto> GetOperationAsync(Guid id, CancellationToken cancellationToken) =>
		history.GetOperationAsync(id, cancellationToken);

	[HttpGet("history")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Notification.View)]
	public Task<PagedResultDto<AcademicNotificationHistoryItemDto>> GetHistoryAsync([FromQuery] GetAcademicNotificationHistoryDto input,
		CancellationToken cancellationToken) => history.GetHistoryAsync(input, cancellationToken);
}
