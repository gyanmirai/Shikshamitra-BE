using System;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Mobile;
using ShikshaMitra.Library.Portal;
using ShikshaMitra.Students.Portal;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task My_Library_Should_List_The_Exact_Finance_Receipt_For_A_Linked_Child()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Student, departed: false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		await ConfigureAndApproveTypedAsync(context);
		using var operatorAccount = SignIn(context.Owner);
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		form.CanCollect.ShouldBeTrue();
		var accepted = await WithUnitOfWorkAsync(() => TypedService(context.Owner).CollectAsync(CashCollectionInput(context, form.Revision)));
		var guardianId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var guardian = new IdentityUser(guardianId, $"guardian-{guardianId:N}", $"{guardianId:N}@example.test", context.Owner.TenantId);
			guardian.SetEmailConfirmed(true);
			await GetRequiredService<IIdentityUserRepository>().InsertAsync(guardian, true);
			await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().InsertAsync(new(Guid.NewGuid(),
				context.Owner.TenantId, context.Owner.PersonId, guardianId, "guardian", "mother", context.Owner.ActorId), true);
			var permissions = GetRequiredService<IPermissionManager>();
			await permissions.SetAsync(ShikshaMitraSubmodulePermissions.Library.Portal.View, "U", guardianId.ToString(), true);
			await permissions.SetAsync(MobileFinancePermissions.ViewReceipts, "U", guardianId.ToString(), true);
		});
		using var owner = SignIn(context.Owner with { ActorId = guardianId });
		var portal = GetRequiredService<ILibraryPortalAppService>();
		(await portal.GetReceiptYearsAsync(context.Owner.PersonId)).ShouldContain(row => row.Id == context.Owner.YearId);
		var results = await portal.GetReceiptsAsync(context.Owner.PersonId, context.Owner.YearId);
		var receipt = results.Items.ShouldHaveSingleItem();
		receipt.Id.ShouldBe(accepted.ReceiptId);
		receipt.Number.ShouldBe(accepted.ReceiptNumber);
		receipt.ReceivedOn.ShouldBe(accepted.SchoolReceiptDate);
		await Should.ThrowAsync<AbpAuthorizationException>(() => portal.GetReceiptsAsync(Guid.NewGuid(), context.Owner.YearId));
	}
}
