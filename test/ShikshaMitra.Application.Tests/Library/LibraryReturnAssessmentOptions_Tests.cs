using System;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReturnAssessmentOptions_Tests : ShikshaMitraApplicationTestBase<LibraryCanonicalLookupTestModule>
{
	[Fact]
	public async Task Return_Operator_Should_Read_Actual_Category_Amount_And_Fixed_Flag_Without_Settings_Or_Charge_Admin_Grants()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var fixedId = await AddAsync(context.Catalog.YearId, "Damage repair", 42.75m, true);
		await AddAsync(context.Catalog.YearId, "Manual assessment", 0m, false);

		var result = await Lookups.GetAssessmentCategoriesAsync(context.Catalog.YearId);

		result.TotalCount.ShouldBe(2);
		var fixedCategory = result.Items.Single(row => row.Id == fixedId);
		fixedCategory.Name.ShouldBe("Damage repair");
		fixedCategory.Amount.ShouldBe(42.75m);
		fixedCategory.IsFixed.ShouldBeTrue();
		fixedCategory.AcademicYearId.ShouldBe(context.Catalog.YearId);
		result.Items.Single(row => !row.IsFixed).Amount.ShouldBe(0m);
	}

	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View)]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Return)]
	public async Task Assessment_Options_Should_Require_Each_Exact_Owning_Grant(string denied)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		GetRequiredService<IPermissionChecker>().IsGrantedAsync(denied).Returns(false);

		await Should.ThrowAsync<AbpAuthorizationException>(() => Lookups.GetAssessmentCategoriesAsync(context.Catalog.YearId));
	}

	[Fact]
	public async Task Assessment_Options_Should_Filter_Tenant_And_Year_Before_Counts_And_Deny_Unauthorized_Or_Foreign_Years()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var own = await AddAsync(context.Catalog.YearId, "Current category", 10m, true);
		await AddAsync(context.Catalog.OriginYearId, "Previous category", 99m, true);
		LibraryCopyLifecycleTestContext foreign;
		using (LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider))
		{
			foreign = await SeedAsync();
			await AddAsync(foreign.Catalog.YearId, "Foreign category", 999m, true);
		}
		await WithUnitOfWorkAsync(async () =>
		{
			var actor = GetRequiredService<ICurrentUser>().Id!.Value;
			var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await assignments.DeleteAsync(row => row.UserId == actor, true);
			await assignments.InsertAsync(new(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, userId: actor,
				academicYearId: context.Catalog.YearId, educationBoardId: context.BoardId, educationMediumId: context.MediumId,
				educationMediumStageId: context.StageId, standardId: context.BorrowerClassId), true);
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actor);
		});

		var result = await Lookups.GetAssessmentCategoriesAsync(context.Catalog.YearId);

		result.TotalCount.ShouldBe(1);
		result.Items.Single().Id.ShouldBe(own);
		await Should.ThrowAsync<BusinessException>(() => Lookups.GetAssessmentCategoriesAsync(context.Catalog.OriginYearId));
		await Should.ThrowAsync<BusinessException>(() => Lookups.GetAssessmentCategoriesAsync(foreign.Catalog.YearId));
		await Should.ThrowAsync<BusinessException>(() => Lookups.GetAssessmentCategoriesAsync(Guid.NewGuid()));
	}

	[Fact]
	public async Task Assessment_Options_Should_Search_And_Page_All_Eligible_Choices_Instead_Of_Truncating_The_Catalogue()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		for (var index = 1; index <= 5; index++) await AddAsync(context.Catalog.YearId, $"Option {index:00}", index + 0.25m, index % 2 == 0);
		await AddAsync(context.Catalog.YearId, "Unrelated", 100m, false);

		var second = await Lookups.GetAssessmentCategoriesAsync(context.Catalog.YearId, page: 2, pageSize: 2, search: "option");

		second.TotalCount.ShouldBe(5);
		second.TotalPages.ShouldBe(3);
		second.CurrentPage.ShouldBe(2);
		second.Items.Select(row => row.Name).ShouldBe(["Option 03", "Option 04"]);
		var first = await Lookups.GetAssessmentCategoriesAsync(context.Catalog.YearId, page: 1, pageSize: 2, search: "Option");
		first.Items.Select(row => row.Id).Intersect(second.Items.Select(row => row.Id)).ShouldBeEmpty();
		(await Lookups.GetAssessmentCategoriesAsync(context.Catalog.YearId, page: 3, pageSize: 2, search: "Option")).Items.Count.ShouldBe(1);
	}

	private ILibraryCirculationLookupAppService Lookups => GetRequiredService<ILibraryCirculationLookupAppService>();
	private Task<LibraryCopyLifecycleTestContext> SeedAsync()
	{
		var permissions = GetRequiredService<IPermissionChecker>();
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_ChargesCategories.View).Returns(false);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View).Returns(false);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Charges.Add).Returns(false);
		return WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
	}
	private Task<Guid> AddAsync(Guid yearId, string name, decimal amount, bool fixedAmount) => WithUnitOfWorkAsync(async () =>
	{
		var row = new LibraryChargeCategory(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, yearId, name, amount, fixedAmount, "Return assessment option");
		await GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().InsertAsync(row, true);
		return row.Id;
	});
}
