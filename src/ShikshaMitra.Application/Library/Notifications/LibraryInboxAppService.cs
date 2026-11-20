using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Library.Books;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library.Notifications;

[Authorize]
[RemoteService(false)]
public class LibraryInboxAppService(
	IRepository<LibraryNotificationHistory, Guid> histories,
	IRepository<LibraryNotificationSetting, Guid> settings,
	IIdentityUserRepository users,
	LibraryNotificationRecipientResolver recipients,
	ILibraryNotificationReminderEligibility eligibility,
	IUnitOfWorkManager units) : ApplicationService, ILibraryInboxAppService
{
	public async Task<LibraryInboxResultDto> GetAsync(GetLibraryInboxRequest input)
	{
		using var read = units.Begin(requiresNew: true, isTransactional: false);
		var query = await AuthorizedQueryAsync();
		if (input.MemberId.HasValue) query = query.Where(row => row.RecipientMemberId == input.MemberId);
		var unread = await AsyncExecuter.CountAsync(query.Where(row => row.ReadAt == null));
		if (input.UnreadOnly) query = query.Where(row => row.ReadAt == null);
		var count = await AsyncExecuter.CountAsync(query);
		var page = Math.Max(1, input.Page);
		var pageSize = Math.Clamp(input.PageSize, 1, 100);
		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(row => row.SentAt).ThenByDescending(row => row.Id)
			.Skip((page - 1) * pageSize).Take(pageSize));
		await read.CompleteAsync();
		return new LibraryInboxResultDto
		{
			Items = rows.Select(Map).ToList(), TotalCount = count, UnreadCount = unread,
			CurrentPage = page, PageSize = pageSize, TotalPages = (int)Math.Ceiling(count / (double)pageSize)
		};
	}

	public async Task<LibraryInboxItemDto> MarkReadAsync(Guid id)
	{
		using var write = units.Begin(requiresNew: true, isTransactional: true);
		var row = await AsyncExecuter.FirstOrDefaultAsync((await AuthorizedQueryAsync()).Where(row => row.Id == id))
			?? throw new AbpAuthorizationException();
		if (!row.ReadAt.HasValue)
		{
			row.MarkRead(CurrentUser.Id!.Value, Clock.Now.ToUniversalTime());
			await histories.UpdateAsync(row, true);
		}
		await write.CompleteAsync();
		return Map(row);
	}

	private async Task<IQueryable<LibraryNotificationHistory>> AuthorizedQueryAsync()
	{
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue) throw new AbpAuthorizationException();
		var user = await users.FindAsync(CurrentUser.Id.Value);
		if (user == null || !user.IsActive || user.TenantId != CurrentTenant.Id) throw new AbpAuthorizationException();
		var members = await recipients.GetInboxMemberAccessAsync(user.Id);
		var memberIds = members.ActiveIds.ToArray();
		var inactiveIds = members.InactiveIds.ToArray();
		var history = (await histories.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id && row.RecipientUserId == user.Id &&
			row.Channel == LibraryNotificationChannels.InApp && row.Status == LibraryNotificationDeliveryStatuses.Sent && row.SentAt != null);
		List<Guid> fineSources = inactiveIds.Length == 0 ? [] : await AsyncExecuter.ToListAsync(history
			.Where(row => row.EventType == LibraryNotificationEventTypes.FineDueReminder && row.RecipientRole == LibraryNotificationRecipientRoles.Member &&
				row.RecipientMemberId.HasValue && Enumerable.Contains(inactiveIds, row.RecipientMemberId.Value) &&
				row.RelatedEntityType == nameof(LibraryCharge) && row.RelatedEntityId.HasValue)
			.Select(row => row.RelatedEntityId!.Value).Distinct());
		var recovery = await eligibility.GetInboxRecoveryScopeAsync(inactiveIds, fineSources);
		var operational = await recipients.CanReadOperationalInboxAsync(user.Id);
		var enabled = await settings.GetListAsync(setting => setting.TenantId == CurrentTenant.Id && setting.Channel == LibraryNotificationChannels.InApp &&
			setting.IsActive && setting.IsChannelAvailable && !setting.IsComingSoon);
		var memberEvents = enabled.Where(setting => setting.RecipientRole == LibraryNotificationRecipientRoles.Member).Select(setting => setting.EventType).Distinct().ToArray();
		var operationalEvents = enabled.Where(setting => setting.RecipientRole == LibraryNotificationRecipientRoles.Librarian).Select(setting => setting.EventType).Distinct().ToArray();
		return history.Where(row => (row.RecipientMemberId.HasValue && row.RecipientRole == LibraryNotificationRecipientRoles.Member && Enumerable.Contains(memberEvents, row.EventType) &&
			  (Enumerable.Contains(memberIds, row.RecipientMemberId.Value) ||
			   (Enumerable.Contains(inactiveIds, row.RecipientMemberId.Value) && row.RelatedEntityId.HasValue &&
			    ((row.EventType == LibraryNotificationEventTypes.BookIssueConfirmation && Enumerable.Contains(recovery.IssueReceiptIds, row.RelatedEntityId.Value)) ||
			     ((row.EventType == LibraryNotificationEventTypes.DueDateReminder || row.EventType == LibraryNotificationEventTypes.OverdueReminder) && Enumerable.Contains(recovery.OpenIssueIds, row.RelatedEntityId.Value)) ||
			     (row.EventType == LibraryNotificationEventTypes.ReturnConfirmation && Enumerable.Contains(recovery.ReturnedIssueIds, row.RelatedEntityId.Value)) ||
			     (row.EventType == LibraryNotificationEventTypes.FineDueReminder && row.RelatedEntityType == nameof(LibraryCharge) &&
				  Enumerable.Contains(recovery.CollectibleChargeIds, row.RelatedEntityId.Value)) ||
			     (row.EventType == LibraryNotificationEventTypes.ReturnChargeReminder && Enumerable.Contains(recovery.ReturnChargeIssueIds, row.RelatedEntityId.Value)))))) ||
			 (!row.RecipientMemberId.HasValue && operational && row.RecipientRole == LibraryNotificationRecipientRoles.Librarian && Enumerable.Contains(operationalEvents, row.EventType)));
	}

	private static LibraryInboxItemDto Map(LibraryNotificationHistory row) => new()
	{
		Id = row.Id, EventType = row.EventType, Subject = row.Subject, Message = row.Message,
		MemberId = row.RecipientMemberId, MemberName = row.ExtraProperties.GetValueOrDefault("MemberName")?.ToString(),
		SentAt = row.SentAt!.Value, ReadAt = row.ReadAt
	};
}
