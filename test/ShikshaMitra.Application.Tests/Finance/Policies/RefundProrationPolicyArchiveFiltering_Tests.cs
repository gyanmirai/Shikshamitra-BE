using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
	[Fact]
	public async Task Collection_List_Filters_And_Sorts_Grace_Days_Before_Paging()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, _, _, head) = await SeedStandardAsync();
		var shortId = await CreateCollectionPolicyAsync(year, head, "Short grace policy", graceDays: 2);
		var longId = await CreateCollectionPolicyAsync(year, head, "Long grace policy", graceDays: 8);

		var filtered = await _collectionPolicies.GetListAsync(new()
		{
			AcademicYearId = year, GraceDaysOperator = "greaterThan", GraceDaysValue = 3
		});
		filtered.TotalCount.ShouldBe(1);
		filtered.Items[0].Id.ShouldBe(longId);

		var firstPage = await _collectionPolicies.GetListAsync(new()
		{
			AcademicYearId = year, Sorting = "gracePeriodDays asc", MaxResultCount = 1
		});
		firstPage.TotalCount.ShouldBe(2);
		firstPage.Items[0].Id.ShouldBe(shortId);
	}

	[Fact]
	public async Task Refund_List_Separates_Current_And_Archived_And_Restores_To_Draft()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, _, _, _) = await SeedStandardAsync();
		var service = GetRequiredService<IRefundPolicyAppService>();
		var draft = await service.CreateAsync(new() { AcademicYearId = year, PolicyName = "Current refund" });
		var active = await service.CreateAsync(new()
		{
			AcademicYearId = year, PolicyName = "Active refund", Status = "Active",
			ExpenseCategoryId = await CreateRefundExpenseCategoryAsync(year), AllowedPayoutMethods = ["Cash"]
		});
		var activeDetails = await service.GetAsync(active.Id);
		activeDetails.ExpenseCategoryName.ShouldNotBeNullOrWhiteSpace();
		var activeVersions = await service.GetVersionsAsync(active.Id, new FinancePolicyVersionListRequestDto());
		using (var recorded = JsonDocument.Parse(activeVersions.Items[0].SnapshotJson))
			recorded.RootElement.GetProperty("ExpenseCategoryName").GetString()
				.ShouldBe(activeDetails.ExpenseCategoryName);
		var archived = await service.CreateAsync(new() { AcademicYearId = year, PolicyName = "Archived refund" });
		await service.ArchiveAsync(archived.Id);
		await service.CreateAsync(new() { AcademicYearId = year, PolicyName = "Other archived refund" });
		var current = await service.GetListAsync(new() { AcademicYearId = year, MaxResultCount = 20 });
		current.TotalCount.ShouldBe(3);
		current.Items.ShouldContain(x => x.Id == draft.Id);
		current.Items.ShouldContain(x => x.Id == active.Id);
		current.Items.ShouldNotContain(x => x.Id == archived.Id);
		var combined = await service.GetListAsync(new() { AcademicYearId = year, IncludeArchived = true, MaxResultCount = 20 });
		combined.TotalCount.ShouldBe(current.TotalCount + 1);
		combined.Items.ShouldContain(x => x.Id == draft.Id);
		combined.Items.ShouldContain(x => x.Id == active.Id);
		combined.Items.ShouldContain(x => x.Id == archived.Id);
		var combinedPage = await service.GetListAsync(new() { AcademicYearId = year, IncludeArchived = true, MaxResultCount = 1 });
		combinedPage.TotalCount.ShouldBe(combined.TotalCount);
		combinedPage.Items.Count.ShouldBe(1);
		var archivedOnly = await service.GetListAsync(new() { AcademicYearId = year, IncludeArchived = true, Status = "Archived" });
		archivedOnly.TotalCount.ShouldBe(1);
		archivedOnly.Items[0].Id.ShouldBe(archived.Id);
		var activePage = await service.GetListAsync(new() { AcademicYearId = year, Status = "Active" });
		activePage.TotalCount.ShouldBe(1);
		activePage.Items[0].Id.ShouldBe(active.Id);
		var payoutPage = await service.GetListAsync(new()
		{
			AcademicYearId = year, AllowedPayoutMethod = "Cheque", Sorting = "allowedPayoutMethods desc"
		});
		payoutPage.TotalCount.ShouldBe(2);
		payoutPage.Items.ShouldNotContain(x => x.Id == active.Id);
		var sortedPayouts = await service.GetListAsync(new()
		{
			AcademicYearId = year, Sorting = "allowedPayoutMethods asc", MaxResultCount = 1
		});
		sortedPayouts.TotalCount.ShouldBe(3);
		sortedPayouts.Items[0].Id.ShouldBe(active.Id);
		var partialPage = await service.GetListAsync(new()
		{
			AcademicYearId = year, AllowPartialRefund = true, PolicyNameFilter = "Current", PolicyNameOperator = "startsWith"
		});
		partialPage.Items.ShouldContain(x => x.Id == draft.Id);
		var archivePage = await service.GetListAsync(new() { AcademicYearId = year, Status = "Archived", Search = "Archived", MaxResultCount = 1 });
		archivePage.TotalCount.ShouldBe(1);
		archivePage.Items[0].Id.ShouldBe(archived.Id);
		(await service.GetListAsync(new() { AcademicYearId = Guid.NewGuid(), Status = "Archived" })).TotalCount.ShouldBe(0);
		(await service.UnarchiveAsync(archived.Id)).Status.ShouldBe("Draft");
		(await service.GetListAsync(new() { AcademicYearId = year })).Items.ShouldContain(x => x.Id == archived.Id);
		(await service.GetListAsync(new() { AcademicYearId = year, Status = "Archived" })).TotalCount.ShouldBe(0);
	}

	[Fact]
	public async Task Proration_List_Separates_Current_And_Archived_And_Restores_To_Draft()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, _, _, head) = await SeedStandardAsync();
		var service = GetRequiredService<IProrationPolicyAppService>();
		CreateUpdateProrationPolicyDto Input(string name) => new()
		{
			AcademicYearId = year, PolicyName = name,
			FeeHeadRules = [new() { FeeHeadId = head, RuleType = "Daily" }]
		};
		var draft = await service.CreateAsync(Input("Current proration"));
		var active = await service.CreateAsync(Input("Active proration"));
		await service.ActivateAsync(active.Id);
		var archived = await service.CreateAsync(Input("Archived proration"));
		await service.ArchiveAsync(archived.Id);
		var current = await service.GetListAsync(new() { AcademicYearId = year, MaxResultCount = 20 });
		current.TotalCount.ShouldBe(2);
		current.Items.ShouldContain(x => x.Id == draft.Id);
		current.Items.ShouldContain(x => x.Id == active.Id);
		current.Items.ShouldNotContain(x => x.Id == archived.Id);
		var combined = await service.GetListAsync(new() { AcademicYearId = year, IncludeArchived = true, MaxResultCount = 20 });
		combined.TotalCount.ShouldBe(current.TotalCount + 1);
		combined.Items.ShouldContain(x => x.Id == draft.Id);
		combined.Items.ShouldContain(x => x.Id == active.Id);
		combined.Items.ShouldContain(x => x.Id == archived.Id);
		var combinedPage = await service.GetListAsync(new() { AcademicYearId = year, IncludeArchived = true, MaxResultCount = 1 });
		combinedPage.TotalCount.ShouldBe(combined.TotalCount);
		combinedPage.Items.Count.ShouldBe(1);
		var archivedOnly = await service.GetListAsync(new() { AcademicYearId = year, IncludeArchived = true, Status = "Archived" });
		archivedOnly.TotalCount.ShouldBe(1);
		archivedOnly.Items[0].Id.ShouldBe(archived.Id);
		var activePage = await service.GetListAsync(new() { AcademicYearId = year, Status = "Active" });
		activePage.TotalCount.ShouldBe(1);
		activePage.Items[0].Id.ShouldBe(active.Id);
		var ruleCountPage = await service.GetListAsync(new()
		{
			AcademicYearId = year, RuleCountOperator = "equals", RuleCountValue = 1,
			Sorting = "feeHeadRules desc", MaxResultCount = 1
		});
		ruleCountPage.TotalCount.ShouldBe(2);
		ruleCountPage.Items.Count.ShouldBe(1);
		var archivePage = await service.GetListAsync(new() { AcademicYearId = year, Status = "Archived", Search = "Archived", MaxResultCount = 1 });
		archivePage.TotalCount.ShouldBe(1);
		archivePage.Items[0].Id.ShouldBe(archived.Id);
		(await service.UnarchiveAsync(archived.Id)).Status.ShouldBe("Draft");
		(await service.GetListAsync(new() { AcademicYearId = year })).Items.ShouldContain(x => x.Id == archived.Id);
		(await service.GetListAsync(new() { AcademicYearId = year, Status = "Archived" })).TotalCount.ShouldBe(0);
	}
}
