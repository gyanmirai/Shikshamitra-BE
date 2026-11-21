using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Charges.Editor;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Security.Permissions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Users;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	private async Task<ChargeEditorLookupFixture> SeedChargeEditorLookupAsync()
	{
		// Existing proven fixture performs a real canonical checkout/capture, creates a reviewed
		// loan-linked assessment, then moves HR into a distinct same-year board and medium.
		var (scope, loan, source) = await SeedMovedStaffFinanceSourceAsync();
		var identifier = await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<Employee, Guid>>().GetAsync(scope.EmployeeId)).EmployeeCode);
		return new(scope, loan, source, identifier);
	}

	private async Task<LibraryChargeEditorLookupAppService> ActualChargeEditorLookupsAsync(params string[] granted)
	{
		var permissions = new[]
		{
			ShikshaMitraSubmodulePermissions.Library.Charges.View,
			ShikshaMitraSubmodulePermissions.Library.Charges.Add,
			ShikshaMitraSubmodulePermissions.Library.Charges.Update,
			ShikshaMitraSubmodulePermissions.Library.Settings_ChargesCategories.View,
			ShikshaMitraSubmodulePermissions.Library.LibraryMembers.View,
			ShikshaMitraSubmodulePermissions.Library.IssueReturn.View,
			ShikshaMitraSubmodulePermissions.Library.IssueReturn.Issue,
			ShikshaMitraSubmodulePermissions.Library.IssueReturn.Return,
			ShikshaMitraSubmodulePermissions.Library.AllBooks.View
		};
		await WithUnitOfWorkAsync(async () =>
		{
			var actorId = GetRequiredService<ICurrentUser>().Id!.Value;
			var actor = await GetRequiredService<IIdentityUserRepository>().GetAsync(actorId);
			actor.TenantId.ShouldBe(GetRequiredService<ICurrentTenant>().Id);
			actor.IsActive.ShouldBeTrue();
			foreach (var permission in permissions)
				await GetRequiredService<IPermissionManager>().SetAsync(permission, "U", actorId.ToString(),
					Enumerable.Contains(granted, permission));
		});
		var checker = ActivatorUtilities.CreateInstance<ShikshaMitraPermissionChecker>(ServiceProvider);
		foreach (var permission in permissions)
			(await checker.IsGrantedAsync(permission)).ShouldBe(Enumerable.Contains(granted, permission), permission);
		// Direct construction deliberately injects the real DB-backed checker. The module-wide
		// test checker or attribute interception cannot supply the action-denial evidence.
		var service = ActivatorUtilities.CreateInstance<LibraryChargeEditorLookupAppService>(ServiceProvider, checker);
		service.LazyServiceProvider = new AbpLazyServiceProvider(ServiceProvider);
		return service;
	}

	private async Task CallChargeEditorLookupAsync(ILibraryChargeEditorLookupAppService service,
		ChargeEditorLookupFixture fixture, string operation)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var year = fixture.Scope.Context.Catalog.YearId;
			switch (operation)
			{
				case "Categories": _ = await service.GetCategoriesAsync(year, search: "Reviewed staff assessment"); break;
				case "SearchMembers": _ = await service.SearchMembersAsync(year, fixture.Identifier); break;
				case "ResolveMember": _ = await service.ResolveMemberAsync(year, fixture.Identifier); break;
				case "LoanSources": _ = await service.GetLoanSourcesAsync(year, fixture.Scope.EmployeeId); break;
				case "UpdateContext": _ = await service.GetUpdateContextAsync(fixture.Source.SourceId); break;
				default: throw new ArgumentOutOfRangeException(nameof(operation));
			}
		});
	}

	private async Task<ChargeEditorCategoryIsolation> SeedChargeEditorCategoryIsolationAsync(ChargeEditorLookupFixture fixture)
	{
		var search = $"ED-{Guid.NewGuid():N}"[..15];
		var ids = await WithUnitOfWorkAsync(async () =>
		{
			var tenant = GetRequiredService<ICurrentTenant>().Id;
			var first = new LibraryChargeCategory(Guid.NewGuid(), tenant, fixture.Scope.Context.Catalog.YearId,
				$"{search} A", 10.25m, false, "Current year first");
			var second = new LibraryChargeCategory(Guid.NewGuid(), tenant, fixture.Scope.Context.Catalog.YearId,
				$"{search} B", 20.50m, true, "Current year second");
			var otherYear = new LibraryChargeCategory(Guid.NewGuid(), tenant, fixture.Scope.Context.Catalog.OriginYearId,
				$"{search} Other year", 99m, true, "Different actual year");
			await GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().InsertManyAsync([first, second, otherYear], true);
			return (FirstId: first.Id, SecondId: second.Id, OtherYearId: otherYear.Id);
		});
		(Guid CategoryId, Guid YearId) foreign;
		using (LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider))
		{
			foreign = await WithUnitOfWorkAsync(async () =>
			{
				var context = await LibraryPolicyTestFixture.SeedAsync(ServiceProvider);
				var category = new LibraryChargeCategory(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id,
					context.YearId, $"{search} Other tenant", 200m, true, "Other actual school and year");
				await GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().InsertAsync(category, true);
				return (CategoryId: category.Id, YearId: context.YearId);
			});
		}
		return new(search, ids.FirstId, ids.SecondId, ids.OtherYearId, foreign.CategoryId, foreign.YearId);
	}

	private sealed record ChargeEditorLookupFixture(StaffScopeFixture Scope, LibraryLoanDto Loan,
		FinanceExternalSourceReference Source, string Identifier);
	private sealed record ChargeEditorCategoryIsolation(string Search, Guid FirstId, Guid SecondId,
		Guid OtherYearId, Guid ForeignTenantId, Guid ForeignYearId);
}
