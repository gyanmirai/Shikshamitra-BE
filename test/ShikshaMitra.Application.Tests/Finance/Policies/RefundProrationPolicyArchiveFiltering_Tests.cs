using System;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
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
		var archived = await service.CreateAsync(new() { AcademicYearId = year, PolicyName = "Archived refund" });
		await service.ArchiveAsync(archived.Id);
		await service.CreateAsync(new() { AcademicYearId = year, PolicyName = "Other archived refund" });
		var current = await service.GetListAsync(new() { AcademicYearId = year, MaxResultCount = 20 });
		current.TotalCount.ShouldBe(3);
		current.Items.ShouldContain(x => x.Id == draft.Id);
		current.Items.ShouldContain(x => x.Id == active.Id);
		current.Items.ShouldNotContain(x => x.Id == archived.Id);
		var activePage = await service.GetListAsync(new() { AcademicYearId = year, Status = "Active" });
		activePage.TotalCount.ShouldBe(1);
		activePage.Items[0].Id.ShouldBe(active.Id);
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
			FeeHeadRules = [new() { FeeHeadId = head, RuleType = "Daily", MinimumChargeDays = 0 }]
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
		var activePage = await service.GetListAsync(new() { AcademicYearId = year, Status = "Active" });
		activePage.TotalCount.ShouldBe(1);
		activePage.Items[0].Id.ShouldBe(active.Id);
		var archivePage = await service.GetListAsync(new() { AcademicYearId = year, Status = "Archived", Search = "Archived", MaxResultCount = 1 });
		archivePage.TotalCount.ShouldBe(1);
		archivePage.Items[0].Id.ShouldBe(archived.Id);
		(await service.UnarchiveAsync(archived.Id)).Status.ShouldBe("Draft");
		(await service.GetListAsync(new() { AcademicYearId = year })).Items.ShouldContain(x => x.Id == archived.Id);
		(await service.GetListAsync(new() { AcademicYearId = year, Status = "Archived" })).TotalCount.ShouldBe(0);
	}
}
