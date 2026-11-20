using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.ExternalReceivables.Operator;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Library.Books;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Theory]
	[InlineData(FinanceExternalPayerKind.Student)]
	[InlineData(FinanceExternalPayerKind.Employee)]
	public async Task Operator_Posting_Options_Should_Page_Actual_Source_Year_Categories_Without_Settings_Or_Fee_Assignments(FinanceExternalPayerKind kind)
	{
		var context = await SeedOperatorChoicesAsync(kind);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Owner);
		var permissions = new FinanceServices(ServiceProvider, context.Owner, useStoredSource: true).GetRequiredService<IPermissionChecker>();
		(await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View)).ShouldBeTrue();
		(await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add)).ShouldBeTrue();
		foreach (var unrelated in new[] { ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ChargeCategory.View,
			ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.View,
			ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.View })
			(await permissions.IsGrantedAsync(unrelated)).ShouldBeFalse();
		var service = OperatorService(context.Owner);
		var first = await WithUnitOfWorkAsync(() => service.GetPostingOptionsAsync(context.Source,
			FinanceExternalOperatorOptionKind.ChargeCategory, page: 1, pageSize: 1, search: context.Search));
		var second = await WithUnitOfWorkAsync(() => service.GetPostingOptionsAsync(context.Source,
			FinanceExternalOperatorOptionKind.ChargeCategory, page: 2, pageSize: 1, search: context.Search));
		var third = await WithUnitOfWorkAsync(() => service.GetPostingOptionsAsync(context.Source,
			FinanceExternalOperatorOptionKind.ChargeCategory, page: 3, pageSize: 1, search: context.Search));
		var end = await WithUnitOfWorkAsync(() => service.GetPostingOptionsAsync(context.Source,
			FinanceExternalOperatorOptionKind.ChargeCategory, page: 4, pageSize: 1, search: context.Search));
		first.TotalCount.ShouldBe(3); second.TotalCount.ShouldBe(3); third.TotalCount.ShouldBe(3); end.TotalCount.ShouldBe(3);
		end.Items.ShouldBeEmpty();
		var found = first.Items.Concat(second.Items).Concat(third.Items).ToList();
		found.Select(row => row.Id).Distinct().Count().ShouldBe(3);
		found.Select(row => row.Id).OrderBy(id => id).ShouldBe(context.VisibleCategories.OrderBy(id => id));
		found.Where(row => row.Id != context.UnmappedCategoryId).ShouldAllBe(row => row.IsSelectable);
		var needsSetup = found.Single(row => row.Id == context.UnmappedCategoryId);
		needsSetup.IsSelectable.ShouldBeFalse(); needsSetup.BlockedReason.ShouldNotBeNullOrWhiteSpace();
		await AssertOperatorChoicesAreReadOnlyAsync(context);
	}

	[Fact]
	public async Task Operator_Posting_Policy_Options_Should_Return_Immutable_Version_Ids_For_Only_The_Actual_Source_Year()
	{
		var context = await SeedOperatorChoicesAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Owner);
		var service = OperatorService(context.Owner);
		var first = await WithUnitOfWorkAsync(() => service.GetPostingOptionsAsync(context.Source,
			FinanceExternalOperatorOptionKind.CollectionPolicyVersion, page: 1, pageSize: 1, search: context.Search));
		var second = await WithUnitOfWorkAsync(() => service.GetPostingOptionsAsync(context.Source,
			FinanceExternalOperatorOptionKind.CollectionPolicyVersion, page: 2, pageSize: 1, search: context.Search));
		var end = await WithUnitOfWorkAsync(() => service.GetPostingOptionsAsync(context.Source,
			FinanceExternalOperatorOptionKind.CollectionPolicyVersion, page: 3, pageSize: 1, search: context.Search));
		first.TotalCount.ShouldBe(2); second.TotalCount.ShouldBe(2); end.TotalCount.ShouldBe(2); end.Items.ShouldBeEmpty();
		var versions = first.Items.Concat(second.Items).ToList();
		versions.Select(row => row.Id).OrderBy(id => id).ShouldBe(context.VisiblePolicyVersions.OrderBy(id => id));
		versions.ShouldAllBe(row => row.IsSelectable && row.PolicyId.HasValue && row.VersionNumber.HasValue &&
			row.VersionNumber.Value > 0 && row.Id != row.PolicyId.Value);
		await WithUnitOfWorkAsync(async () =>
		{
			foreach (var choice in versions)
			{
				var stored = await GetRequiredService<IRepository<FinancePolicyVersion, Guid>>().GetAsync(choice.Id);
				stored.PolicyId.ShouldBe(choice.PolicyId!.Value); stored.VersionNumber.ShouldBe(choice.VersionNumber!.Value);
				stored.TenantId.ShouldBe(context.Owner.TenantId); stored.AcademicYearId.ShouldBe(context.Owner.YearId);
				stored.PolicyType.ShouldBe("collection"); stored.Status.ShouldBe("Active");
			}
		});
		await AssertOperatorChoicesAreReadOnlyAsync(context);
	}

	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.Charges.View)]
	[InlineData(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add)]
	public async Task Operator_Posting_Options_Should_Require_The_Current_Exact_Finance_And_Library_Grants(string denied)
	{
		var context = await SeedOperatorChoicesAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Owner);
		await WithUnitOfWorkAsync(async () =>
		{
			var identity = await GetRequiredService<IIdentityUserRepository>().GetAsync(context.Owner.ActorId);
			foreach (var membership in identity.Roles)
			{
				var role = await GetRequiredService<IIdentityRoleRepository>().GetAsync(membership.RoleId);
				await GetRequiredService<IPermissionManager>().SetAsync(denied, "R", role.Name, false);
			}
		});
		var permission = new FinanceServices(ServiceProvider, context.Owner, useStoredSource: true).GetRequiredService<IPermissionChecker>();
		(await permission.IsGrantedAsync(denied)).ShouldBeFalse();
		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() =>
			OperatorService(context.Owner).GetPostingOptionsAsync(context.Source,
				FinanceExternalOperatorOptionKind.ChargeCategory, search: context.Search)));
		await AssertOperatorChoicesAreReadOnlyAsync(context);
	}
}
