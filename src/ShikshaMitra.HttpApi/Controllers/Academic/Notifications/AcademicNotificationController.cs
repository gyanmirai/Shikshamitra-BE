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
[Route("api/academic/notifications")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AcademicNotificationController(IAcademicNotificationCatalogueAppService catalogue, IAcademicNotificationRuleAppService rules,
	IAcademicNotificationManualAppService manual, IAcademicNotificationHistoryAppService history,
	IAcademicNotificationDeliveryActionAppService deliveryActions) : AbpControllerBase
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

	[HttpGet("manual/student-360-audience/{studentId:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Notification.View)]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Notification.Send)]
	public Task<Student360NotificationAudienceDto> GetStudent360AudienceAsync(Guid studentId, [FromQuery] string eventKey,
		CancellationToken cancellationToken) => manual.GetStudent360AudienceAsync(studentId, eventKey, cancellationToken);

	[HttpGet("manual/diary-follow-up-recipients/{diaryId:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Notification.View)]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Notification.Send)]
	public Task<IReadOnlyList<NotificationRecipientRefDto>> GetDiaryFollowUpRecipientsAsync(Guid diaryId,
		CancellationToken cancellationToken) => manual.GetDiaryFollowUpRecipientsAsync(diaryId, cancellationToken);

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

	[HttpPost("dispatches/{id:guid}/retry")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Notification.Add)]
	public Task<AcademicNotificationRetryDto> RetryDispatchAsync(Guid id, [FromQuery] Guid? expectedAttemptId,
		CancellationToken cancellationToken) => deliveryActions.RetryAsync(id, expectedAttemptId, cancellationToken);

	[HttpPost("operations/{id:guid}/cancel")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Notification.Add)]
	public Task<AcademicNotificationCancellationDto> CancelOperationAsync(Guid id, CancellationToken cancellationToken) =>
		deliveryActions.CancelAsync(id, cancellationToken);
}
