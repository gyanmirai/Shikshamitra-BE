using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Communication.Notifications;

public sealed class GetNotificationInboxInput : PagedResultRequestDto
{
	public bool UnreadOnly { get; set; }
}
public sealed record NotificationInboxItemDto(Guid Id, string Title, string Body, string? RelativeLink,
	DateTime CreationTime, DateTimeOffset? ReadAt, DateTimeOffset? AcknowledgedAt);
public sealed record NotificationUnreadCountDto(long UnreadCount);
public sealed record SaveNotificationPreferenceDto(string Category, Guid? StudentId,
	IReadOnlyList<CommunicationChannelType> EnabledChannels, string LanguageCode, bool QuietHoursEnabled,
	string? QuietHoursStart, string? QuietHoursEnd, string? ConcurrencyStamp = null);
public sealed record NotificationPreferenceDto(string Category, Guid? StudentId,
	IReadOnlyList<CommunicationChannelType> EnabledChannels, string LanguageCode, bool QuietHoursEnabled,
	string? QuietHoursStart, string? QuietHoursEnd, string ConcurrencyStamp);
public sealed record NotificationPreferenceCategoryDto(string Category, IReadOnlyList<CommunicationChannelType> Channels);
public sealed record NotificationPreferenceStudentDto(Guid StudentId, string DisplayName);
public sealed record NotificationPreferenceOptionsDto(IReadOnlyList<NotificationPreferenceCategoryDto> Categories,
	IReadOnlyList<NotificationPreferenceStudentDto> Students);

public interface INotificationInboxAppService : IApplicationService
{
	Task<PagedResultDto<NotificationInboxItemDto>> GetListAsync(GetNotificationInboxInput input, CancellationToken cancellationToken = default);
	Task<NotificationUnreadCountDto> GetUnreadCountAsync(CancellationToken cancellationToken = default);
	Task<NotificationInboxItemDto> MarkReadAsync(Guid id, CancellationToken cancellationToken = default);
	Task<NotificationInboxItemDto> AcknowledgeAsync(Guid id, CancellationToken cancellationToken = default);
}
public interface INotificationPreferenceAppService : IApplicationService
{
	Task<IReadOnlyList<NotificationPreferenceDto>> GetListAsync(CancellationToken cancellationToken = default);
	Task<NotificationPreferenceOptionsDto> GetOptionsAsync(CancellationToken cancellationToken = default);
	Task<NotificationPreferenceDto> SaveAsync(SaveNotificationPreferenceDto input, CancellationToken cancellationToken = default);
}
