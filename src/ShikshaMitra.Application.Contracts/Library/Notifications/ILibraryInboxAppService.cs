using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Notifications;

public interface ILibraryInboxAppService : IApplicationService
{
	Task<LibraryInboxResultDto> GetAsync(GetLibraryInboxRequest input);
	Task<LibraryInboxItemDto> MarkReadAsync(Guid id);
}

public class GetLibraryInboxRequest
{
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 20;
	public bool UnreadOnly { get; set; }
	public Guid? MemberId { get; set; }
}

public class LibraryInboxResultDto : PaginatedResponse<LibraryInboxItemDto>
{
	public long UnreadCount { get; set; }
}

public class LibraryInboxItemDto
{
	public Guid Id { get; set; }
	public string EventType { get; set; } = string.Empty;
	public string Subject { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
	public Guid? MemberId { get; set; }
	public string? MemberName { get; set; }
	public DateTime SentAt { get; set; }
	public DateTime? ReadAt { get; set; }
}
