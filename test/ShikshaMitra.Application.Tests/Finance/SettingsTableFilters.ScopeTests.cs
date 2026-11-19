using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Finance.Settings;

public partial class SettingsTableFilters_Tests
{
	[Fact]
	public async Task Configured_fraction_uses_distinct_configured_classes_within_user_scope()
	{
		using var identityScope = BeginPrincipal();
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var year = await YearAsync();
		var metadataId = await SeedAsync("structure", year, "Scoped structure", 1);
		var service = GetRequiredService<IFeeStructureMetadataAppService>();
		var initial = (await service.GetListAsync(year)).Items.Single();
		var visible = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(initial.Classes.Single().Id);
		var hidden = new Standard(Guid.NewGuid(), tenantId, "Hidden class", 2, visible.EducationMediumStageId, null);
		await InsertAsync(hidden);
		var category = new FeeCategory(Guid.NewGuid(), tenantId, year, "Category", null);
		await InsertAsync(category);
		var incomeId = await FeeHeadAccountingTestData.CreateIncomeCategoryAsync(
			GetRequiredService<IRepository<AccountHead, Guid>>(), GetRequiredService<IRepository<TransactionCategory, Guid>>(), year, tenantId, "Structure filters", DateTimeOffset.UtcNow);
		var head = new FeeHead(Guid.NewGuid(), tenantId, year, category.Id, "Head", FeeHeadModule.Finance, true, null, incomeId);
		await InsertAsync(head);
		for (var index = 0; index < 2; index++)
		{
			var structure = new FeeStructure(Guid.NewGuid(), tenantId, metadataId, visible.Id, $"Structure {index}");
			structure.FeeStructureDetails.Add(new FeeStructureDetail(Guid.NewGuid(), tenantId, structure.Id, head.Id, 100m, false, false, null, null));
			await InsertAsync(structure);
		}
		(await ListAsync("structure", year, Filter("totalClassValue", "1/2"))).Ids.Single().ShouldBe(metadataId);
		(await ListAsync("structure", year, Filter("totalClassValue", "2/2"))).Total.ShouldBe(0);

		var userId = Guid.NewGuid();
		await InsertAsync(new IdentityUser(userId, "settings-filter", "settings-filter@example.test", tenantId));
		await InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: userId, academicYearId: year, standardId: visible.Id));
		using var principal = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, userId.ToString()), new Claim(AbpClaimTypes.TenantId, tenantId.ToString())], "Test")));
		await WithUnitOfWorkAsync(async () =>
		{
			var resolved = await GetRequiredService<IUserScopeService>().GetUserScopeAsync(userId);
			resolved.UserId.ShouldBe(userId);
			resolved.TenantId.ShouldBe(tenantId);
			resolved.AcademicYearIds.ShouldBe(new[] { year });
			resolved.StandardIds.ShouldBe(new[] { visible.Id });
			resolved.HasTenantWideScope().ShouldBeFalse();
		});
		var scoped = await service.GetListAsync(year, filters: Filter("totalClassValue", "1/1"));
		scoped.TotalCount.ShouldBe(1);
		scoped.Items.Single().Classes.Single().Id.ShouldBe(visible.Id);
		(await service.GetListAsync(year, filters: Filter("totalClassValue", "1/2"))).TotalCount.ShouldBe(0);
	}

	[Theory]
	[InlineData("income")]
	[InlineData("expense")]
	[InlineData("bank")]
	[InlineData("charge")]
	[InlineData("concession")]
	[InlineData("structure")]
	public async Task Filters_do_not_reveal_matching_rows_from_another_tenant(string screen)
	{
		using var identityScope = BeginPrincipal();
		Guid foreignYear;
		using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
		{
			foreignYear = await YearAsync();
			await SeedAsync(screen, foreignYear, "Foreign", 1);
		}
		var name = screen == "bank" ? "bankName" : "name";
		(await ListAsync(screen, foreignYear, Filter(name, "Foreign"))).Total.ShouldBe(0);
	}
}
