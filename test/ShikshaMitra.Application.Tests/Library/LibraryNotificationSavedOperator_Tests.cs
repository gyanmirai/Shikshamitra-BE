using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Library.Notifications;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryNotificationRecipientAccess_Tests
{
	[Fact]
	public async Task Notification_Settings_Should_Hydrate_Exact_Saved_Accounts_And_Retain_Unavailable_Ids()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var foreignId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () => {
			using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
				await GetRequiredService<IIdentityUserRepository>().InsertAsync(new IdentityUser(foreignId, "foreign-private-name", "private@example.test", GetRequiredService<ICurrentTenant>().Id), true);
			await GetRequiredService<ISettingManager>().SetForTenantAsync(context.TenantId, LibraryNotificationSettingNames.OperationalRecipientUserIds,
				JsonSerializer.Serialize(new[] { context.GuardianId, context.ActorId, foreignId }));
		});
		var setup = (await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationAppService>().GetSettingsAsync())).Setup;
		setup.OperationalRecipientUserIds.ShouldBe(new[] { context.GuardianId, context.ActorId, foreignId });
		setup.ConfiguredOperationalRecipients.Select(row => row.UserId).ShouldBe(setup.OperationalRecipientUserIds);
		var actual = setup.ConfiguredOperationalRecipients.Single(row => row.UserId == context.ActorId);
		actual.UserName.ShouldStartWith("librarian-"); actual.IsEligible.ShouldBeTrue();
		var unavailable = setup.ConfiguredOperationalRecipients.Single(row => row.UserId == foreignId);
		unavailable.UserName.ShouldBeNull(); unavailable.DisplayName.ShouldBeNull(); unavailable.IsEligible.ShouldBeFalse();
		unavailable.UnavailableReason.ShouldNotBeNullOrWhiteSpace();
		setup.ConfiguredOperationalRecipients.Single(row => row.UserId == context.GuardianId).IsEligible.ShouldBeFalse();
	}

	[Fact]
	public async Task Notification_Settings_Should_Not_Hydrate_Saved_Account_Details_For_A_Scoped_Actor()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		await WithUnitOfWorkAsync(() => GetRequiredService<ISettingManager>().SetForTenantAsync(context.TenantId,
			LibraryNotificationSettingNames.OperationalRecipientUserIds, JsonSerializer.Serialize(new[] { context.ActorId })));
		await LimitRecipientActorToStageAsync(context);
		var result = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationAppService>().GetSettingsAsync());
		result.CanManageSettings.ShouldBeFalse();
		result.Setup.ConfiguredOperationalRecipients.ShouldBeEmpty();
	}
}
