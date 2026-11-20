using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Notifications;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryNotificationRecipientAccess_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Notification_Stored_Scope_Should_Reject_Dispatch_For_A_Prior_Year_Record_Of_A_Current_Year_Member(bool reservation)
	{
		var context = await SeedAsync(); using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var row = await SeedPriorYearNotificationAsync(context, reservation);
		var authority = Services().Get<LibraryNotificationAuthorityService>();
		(await authority.CanDispatchAsync(row)).ShouldBeTrue("Tenant-wide access is the positive control.");
		await LimitNotificationActorToCurrentYearAsync(context);
		(await Services().Get<LibraryNotificationAuthorityService>().CanDispatchAsync(row)).ShouldBeFalse();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Notification_Stored_Scope_Should_Exclude_Prior_Year_History_Rows_And_Counts_Despite_Current_Member_Access(bool reservation)
	{
		var context = await SeedAsync(); using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var row = await SeedPriorYearNotificationAsync(context, reservation);
		var input = new LibraryNotificationHistoryRequest { EventType = row.EventType };
		(await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationAppService>().GetHistoryAsync(input))).TotalCount.ShouldBe(1);
		await LimitNotificationActorToCurrentYearAsync(context);
		var result = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationAppService>().GetHistoryAsync(input));
		result.TotalCount.ShouldBe(0);
		result.Items.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Notification_Stored_Scope_Should_Not_Queue_A_Retry_For_A_Prior_Year_Record_Of_A_Current_Member(bool reservation)
	{
		var context = await SeedAsync(); using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var row = await SeedPriorYearNotificationAsync(context, reservation);
		await LimitNotificationActorToCurrentYearAsync(context);
		var error = await Record.ExceptionAsync(() => WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationAppService>().RetryAsync(row.Id, new())));
		error.ShouldNotBeNull();
		(error is AbpAuthorizationException or EntityNotFoundException).ShouldBeTrue("Out-of-scope history must remain opaque or forbidden.");
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<LibraryNotificationHistory, Guid>>().GetAsync(row.Id))).Status
			.ShouldBe(LibraryNotificationDeliveryStatuses.Failed);
	}
}
