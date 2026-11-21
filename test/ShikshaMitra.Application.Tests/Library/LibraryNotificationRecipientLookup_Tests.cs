using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Notifications;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryNotificationRecipientAccess_Tests
{
	[Fact]
	public async Task Notification_Settings_Should_Expose_Actual_Global_Management_Authority()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		(await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationAppService>().GetSettingsAsync())).CanManageSettings.ShouldBeTrue();
	}

	[Fact]
	public async Task Notification_Settings_Should_Not_Treat_Add_And_A_Scoped_Assignment_As_Global_Authority()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		await LimitRecipientActorToStageAsync(context);
		(await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationAppService>().GetSettingsAsync())).CanManageSettings.ShouldBeFalse();
	}

	[Fact]
	public async Task Notification_Settings_Should_Recheck_Revoked_Add_Grant()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		await WithUnitOfWorkAsync(() => GetRequiredService<IPermissionManager>().SetAsync(Send, "U", context.ActorId.ToString(), false));
		(await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationAppService>().GetSettingsAsync())).CanManageSettings.ShouldBeFalse();
	}

	[Fact]
	public async Task Notification_Recipient_Search_Should_Resolve_Printed_Identifiers_Without_Unrelated_Grants()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var services = Services();
		(await services.Get<IPermissionChecker>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.LibraryMembers.View)).ShouldBeFalse();
		var identifier = await RecipientIdentifierAsync(context);
		var result = await services.Get<LibraryNotificationRecipientLookupAppService>().SearchAsync(new() { Search = identifier });
		result.TotalCount.ShouldBe(1);
		var row = result.Items.Single();
		row.Id.ShouldBe(context.StudentId);
		row.MemberIdentifier.ShouldBe(identifier);
		row.Role.ShouldBe(0);
		row.IsActiveMembership.ShouldBeTrue();
		row.IsEligibleForNotification.ShouldBeTrue();
	}

	[Fact]
	public async Task Notification_Recipient_Search_Should_Explain_Inactive_Membership_Without_Inventing_Eligibility()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		await DeactivateRecipientAsync(context);
		var result = await SearchRecipientAsync(context);
		var row = result.Items.Single();
		row.IsActiveMembership.ShouldBeFalse();
		row.HasRecoverableObligation.ShouldBeFalse();
		row.IsEligibleForNotification.ShouldBeFalse();
		row.EligibilityReason.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task Notification_Recipient_Search_Should_Keep_A_Legacy_Alias_Open_Loan_Follow_Up_Eligible()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		await DeactivateRecipientAsync(context);
		await SeedIssueAsync(context, context.MemberId, new DateOnly(2026, 11, 18));
		var row = (await SearchRecipientAsync(context)).Items.Single();
		row.Id.ShouldBe(context.StudentId);
		row.IsActiveMembership.ShouldBeFalse();
		row.HasRecoverableObligation.ShouldBeTrue();
		row.IsEligibleForNotification.ShouldBeTrue();
		row.EligibilityReason.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task Notification_Recipient_Search_Should_Keep_A_Real_Pending_Charge_Follow_Up_Eligible()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		await DeactivateRecipientAsync(context);
		await SeedRecipientChargeAsync(context);
		var row = (await SearchRecipientAsync(context)).Items.Single();
		row.IsActiveMembership.ShouldBeFalse();
		row.HasRecoverableObligation.ShouldBeTrue();
		row.IsEligibleForNotification.ShouldBeTrue();
	}

	[Fact]
	public async Task Notification_Recipient_Search_Should_Exclude_Another_Stage_From_Items_And_Counts()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var outsideId = await SeedOutsideRecipientAsync(context);
		await LimitRecipientActorToStageAsync(context);
		var result = await Services().Get<LibraryNotificationRecipientLookupAppService>().SearchAsync(new() { PageSize = 1 });
		result.TotalCount.ShouldBe(1);
		result.TotalPages.ShouldBe(1);
		result.Items.Single().Id.ShouldBe(context.StudentId);
		(await Services().Get<LibraryNotificationRecipientLookupAppService>().SearchAsync(new() { Search = outsideId.ToString() })).TotalCount.ShouldBe(0);
	}

	[Fact]
	public async Task Notification_Recipient_Search_Should_Reject_A_Revoked_Add_Grant()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		await WithUnitOfWorkAsync(() => GetRequiredService<IPermissionManager>().SetAsync(Send, "U", context.ActorId.ToString(), false));
		await Should.ThrowAsync<AbpAuthorizationException>(() => SearchRecipientAsync(context));
	}
}
