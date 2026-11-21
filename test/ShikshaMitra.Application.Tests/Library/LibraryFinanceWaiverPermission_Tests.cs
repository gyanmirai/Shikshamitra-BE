using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApproveWaiver)]
	[InlineData(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.RejectWaiver)]
	[InlineData(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ReverseWaiver)]
	public async Task Typed_Waiver_Actions_Should_Be_Defined_And_Require_Their_Own_Exact_Database_Grant(string action)
	{
		var fixture = await SeedAsync(FinanceExternalPayerKind.Student, false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = SignIn(fixture);
		var definition = await GetRequiredService<IPermissionDefinitionManager>().GetAsync(action);
		definition.Name.ShouldBe(action);
		definition.IsEnabled.ShouldBeTrue();
		ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.All.ShouldContain(action);
		var roleName = $"external-finance-{fixture.ActorId:N}";
		await WithUnitOfWorkAsync(() => GetRequiredService<IPermissionManager>().SetAsync(
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add, "R", roleName, true));
		(await ReadTypedGrantAsync(fixture, ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add)).ShouldBeTrue();
		(await ReadTypedGrantAsync(fixture, action)).ShouldBeFalse();

		await WithUnitOfWorkAsync(() => GetRequiredService<IPermissionManager>().SetAsync(action, "R", roleName, true));
		(await ReadTypedGrantAsync(fixture, action)).ShouldBeTrue();
		await WithUnitOfWorkAsync(() => GetRequiredService<IPermissionManager>().SetAsync(
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add, "R", roleName, false));
		(await ReadTypedGrantAsync(fixture, action)).ShouldBeTrue();
	}

	private Task<bool> ReadTypedGrantAsync(Fixture fixture, string action) => WithUnitOfWorkAsync(() =>
		((IPermissionChecker)new FinanceServices(ServiceProvider, fixture).GetService(typeof(IPermissionChecker))!).IsGrantedAsync(action));
}
