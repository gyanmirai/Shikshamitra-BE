using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	// M12: known-ID reads still need this entry point's exact own action and
	// current tenant/profile gate, not an unrelated Library/Finance grant.
	[Fact]
	public async Task Member_Charge_History_Should_Enforce_Exact_View_And_The_Foreign_Target_Gate()
	{
		var foreign = await SeedAsync(FinanceExternalPayerKind.Employee, departed: false);
		var owner = await SeedAsync(FinanceExternalPayerKind.Employee, departed: false);
		foreign.TenantId.ShouldNotBe(owner.TenantId);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var denied = await RestrictMemberChargeReadAsync(owner);
		await AssertMemberChargeReadAuthorityAsync(owner, denied);
		await AssertMemberChargeTargetAsync(owner);
		var authorized = await WithUnitOfWorkAsync(() => MemberChargeReadService(owner).GetMemberChargesAsync(owner.PersonId, 1, 1));
		authorized.TotalCount.ShouldBe(1);
		authorized.Items.ShouldHaveSingleItem().Id.ShouldBe(owner.AssessmentId);

		var outside = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			MemberChargeReadService(owner).GetMemberChargesAsync(foreign.PersonId, 1, 1)));
		outside.Code.ShouldBe(LibraryErrorCodes.MemberNotFound);

		await WithUnitOfWorkAsync(async () =>
		{
			var manager = GetRequiredService<IPermissionManager>();
			var role = $"external-finance-{owner.ActorId:N}";
			await manager.SetAsync(ShikshaMitraSubmodulePermissions.Library.LibraryMembers.View, "R", role, false);
			await manager.SetAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View, "R", role, true);
			await manager.SetAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View, "R", role, true);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var checker = new FinanceServices(ServiceProvider, owner, useStoredSource: true).GetRequiredService<IPermissionChecker>();
			(await checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.LibraryMembers.View)).ShouldBeFalse();
			(await checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View)).ShouldBeTrue();
			(await checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)).ShouldBeTrue();
		});
		// Scope and current target stay valid after the exact action is revoked.
		await AssertMemberChargeTargetAsync(owner);

		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() =>
			MemberChargeReadService(owner).GetMemberChargesAsync(owner.PersonId, 1, 1)));
	}
}

