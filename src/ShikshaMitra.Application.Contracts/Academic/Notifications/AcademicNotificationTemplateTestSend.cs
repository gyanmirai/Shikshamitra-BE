using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Notifications;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AcademicNotificationTemplateTestSendInput
{
	public required Guid RevisionId { get; init; }
	[Required, StringLength(40)]
	public required string ConcurrencyStamp { get; init; }
}

public sealed record AcademicNotificationTemplateTestSendDto(Guid OperationId, CommunicationCampaignStatus Status,
	CommunicationChannelType Channel, bool IsSynthetic);

public interface IAcademicNotificationTemplateTestSendAppService : IApplicationService
{
	Task<AcademicNotificationTemplateTestSendDto> SendAsync(Guid templateId, AcademicNotificationTemplateTestSendInput input,
		CancellationToken cancellationToken = default);
}
