using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Curriculum;
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
	IRepository<CommunicationTemplateRevision, Guid> revisions, IRepository<CommunicationCampaign, Guid> campaigns,
	IRepository<LessonPlan, Guid> lessonPlans) : ApplicationService, INotificationInboxAppService
{
	private sealed class OwnedItem
	{
		public required NotificationInboxItem Item { get; init; }
		public required string EventKey { get; init; }
		public Guid? CampaignId { get; init; }
	}

	private sealed class InboxEntry
	{
		public required NotificationInboxItem Item { get; init; }
		public bool RequiresReview { get; init; }
		public bool RequiresAcknowledgement { get; init; }
	}

	public virtual async Task<PagedResultDto<NotificationInboxItemDto>> GetListAsync(GetNotificationInboxInput input, CancellationToken cancellationToken = default)
	{
		var query = await GetOwnedQueryAsync(cancellationToken);
		if (input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100) throw new BusinessException(NotificationErrorCodes.RecipientIneligible);
		if (input.UnreadOnly) query = query.Where(x => !x.Item.ReadAt.HasValue);
		var count = await AsyncExecuter.LongCountAsync(query, cancellationToken);
		var entries = await WithActionsAsync(query);
		var ordered = input.PrioritizeActions
			? entries.OrderByDescending(x => x.RequiresReview || (x.RequiresAcknowledgement && !x.Item.AcknowledgedAt.HasValue))
				.ThenByDescending(x => x.Item.CreationTime)
			: entries.OrderByDescending(x => x.Item.CreationTime);
		var rows = await AsyncExecuter.ToListAsync(ordered.ThenByDescending(x => x.Item.Id).Skip(input.SkipCount)
			.Take(input.MaxResultCount), cancellationToken);
		return new(count, rows.Select(Map).ToArray());
	}

	public virtual async Task<NotificationUnreadCountDto> GetUnreadCountAsync(CancellationToken cancellationToken = default) =>
		new(await AsyncExecuter.LongCountAsync((await GetOwnedQueryAsync(cancellationToken)).Where(x => !x.Item.ReadAt.HasValue), cancellationToken));

	[UnitOfWork(true)]
	public virtual Task<NotificationInboxItemDto> MarkReadAsync(Guid id, CancellationToken cancellationToken = default) => ChangeStateAsync(id, false, cancellationToken);

	[UnitOfWork(true)]
	public virtual Task<NotificationInboxItemDto> AcknowledgeAsync(Guid id, CancellationToken cancellationToken = default) => ChangeStateAsync(id, true, cancellationToken);

	private async Task<NotificationInboxItemDto> ChangeStateAsync(Guid id, bool acknowledge, CancellationToken cancellationToken)
	{
		var owned = (await GetOwnedQueryAsync(cancellationToken)).Where(x => x.Item.Id == id);
		var entry = await AsyncExecuter.FirstOrDefaultAsync(await WithActionsAsync(owned), cancellationToken)
			?? throw new EntityNotFoundException(typeof(NotificationInboxItem), id);
		var row = entry.Item;
		if (acknowledge) row.Acknowledge(DateTimeOffset.UtcNow);
		else row.MarkRead(DateTimeOffset.UtcNow);
		await inbox.UpdateAsync(row, autoSave: true, cancellationToken: cancellationToken);
		return Map(entry);
	}

	private static NotificationInboxItemDto Map(InboxEntry entry)
	{
		var row = entry.Item;
		return new(row.Id, row.Title, row.Body, SafeLink(row.RelativeLink), row.CreationTime, row.ReadAt,
			row.AcknowledgedAt, entry.RequiresReview, entry.RequiresAcknowledgement);
	}

	private async Task<IQueryable<InboxEntry>> WithActionsAsync(IQueryable<OwnedItem> items)
	{
		var plans = await lessonPlans.GetQueryableAsync();
		var campaignQuery = await campaigns.GetQueryableAsync();
		return items.Select(x => new InboxEntry
		{
			Item = x.Item,
			RequiresReview = x.EventKey == AcademicNotificationEventKeys.LessonPlanChangesRequested &&
				x.Item.SourceType != NotificationCampaignTypes.AcademicTemplateTestSource &&
				plans.Any(plan => plan.Id == x.Item.SourceId && plan.TenantId == x.Item.TenantId && !plan.IsDeleted &&
					plan.Status == LessonPlanStatus.NeedsChanges && plan.ReviewedAt <= x.Item.CreationTime),
			RequiresAcknowledgement = campaignQuery.Any(campaign => campaign.Id == x.CampaignId &&
				campaign.TenantId == x.Item.TenantId && !campaign.IsDeleted && campaign.RequiresAcknowledgement)
		});
	}

	private async Task<IQueryable<OwnedItem>> GetOwnedQueryAsync(CancellationToken cancellationToken)
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
			select new OwnedItem { Item = item, EventKey = revision.EventKey, CampaignId = dispatch.CampaignId };
	}

	private static string? SafeLink(string value) => value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal) &&
		!value.Contains('\\') && !value.Any(char.IsControl) && Uri.TryCreate(value, UriKind.Relative, out _) ? value : null;
}
