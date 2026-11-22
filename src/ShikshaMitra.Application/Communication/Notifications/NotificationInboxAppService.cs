using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Uow;

namespace ShikshaMitra.Communication.Notifications;

[Authorize, RemoteService(false)]
public class NotificationInboxAppService(NotificationSelfIdentity identity, NotificationPersonalStudentAccess students,
	IRepository<NotificationInboxItem, Guid> inbox, IRepository<NotificationDispatch, Guid> dispatches,
	IRepository<CommunicationTemplateRevision, Guid> revisions) : ApplicationService, INotificationInboxAppService
{
	public virtual async Task<PagedResultDto<NotificationInboxItemDto>> GetListAsync(GetNotificationInboxInput input, CancellationToken cancellationToken = default)
	{
		var query = await GetOwnedQueryAsync(cancellationToken);
		if (input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100) throw new BusinessException(NotificationErrorCodes.RecipientIneligible);
		if (input.UnreadOnly) query = query.Where(x => !x.ReadAt.HasValue);
		var count = await AsyncExecuter.LongCountAsync(query, cancellationToken);
		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime).ThenByDescending(x => x.Id)
			.Skip(input.SkipCount).Take(input.MaxResultCount), cancellationToken);
		return new(count, rows.Select(Map).ToArray());
	}

	public virtual async Task<NotificationUnreadCountDto> GetUnreadCountAsync(CancellationToken cancellationToken = default) =>
		new(await AsyncExecuter.LongCountAsync((await GetOwnedQueryAsync(cancellationToken)).Where(x => !x.ReadAt.HasValue), cancellationToken));

	[UnitOfWork(true)]
	public virtual Task<NotificationInboxItemDto> MarkReadAsync(Guid id, CancellationToken cancellationToken = default) => ChangeStateAsync(id, false, cancellationToken);

	[UnitOfWork(true)]
	public virtual Task<NotificationInboxItemDto> AcknowledgeAsync(Guid id, CancellationToken cancellationToken = default) => ChangeStateAsync(id, true, cancellationToken);

	private async Task<NotificationInboxItemDto> ChangeStateAsync(Guid id, bool acknowledge, CancellationToken cancellationToken)
	{
		var row = await AsyncExecuter.FirstOrDefaultAsync((await GetOwnedQueryAsync(cancellationToken)).Where(x => x.Id == id), cancellationToken)
			?? throw new EntityNotFoundException(typeof(NotificationInboxItem), id);
		if (acknowledge) row.Acknowledge(DateTimeOffset.UtcNow);
		else row.MarkRead(DateTimeOffset.UtcNow);
		await inbox.UpdateAsync(row, autoSave: true, cancellationToken: cancellationToken);
		return Map(row);
	}

	private static NotificationInboxItemDto Map(NotificationInboxItem row) => new(row.Id, row.Title, row.Body, SafeLink(row.RelativeLink),
		row.CreationTime, row.ReadAt, row.AcknowledgedAt);

	private async Task<IQueryable<NotificationInboxItem>> GetOwnedQueryAsync(CancellationToken cancellationToken)
	{
		var owner = await identity.RequireAsync(cancellationToken);
		var guardians = await students.GetStudentIdsQueryAsync(owner, "guardian");
		var self = await students.GetStudentIdsQueryAsync(owner, "student");
		return from item in await inbox.GetQueryableAsync()
			join dispatch in await dispatches.GetQueryableAsync() on item.DispatchId equals dispatch.Id
			join revision in await revisions.GetQueryableAsync() on dispatch.TemplateRevisionId equals revision.Id
			where item.TenantId == owner.TenantId && item.UserId == owner.UserId && !item.IsDeleted &&
				dispatch.TenantId == owner.TenantId && !dispatch.IsDeleted && revision.TenantId == owner.TenantId &&
				!revision.IsDeleted &&
				(item.SourceType == NotificationCampaignTypes.AcademicTemplateTestSource ? !item.StudentId.HasValue :
				 revision.IsPublished && (revision.RecipientKind == "Guardian" ? item.StudentId.HasValue && guardians.Contains(item.StudentId.Value) :
				 revision.RecipientKind == "StudentAccount" ? item.StudentId.HasValue && self.Contains(item.StudentId.Value) : true))
			select item;
	}

	private static string? SafeLink(string value) => value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal) &&
		!value.Contains('\\') && !value.Any(char.IsControl) && Uri.TryCreate(value, UriKind.Relative, out _) ? value : null;
}
