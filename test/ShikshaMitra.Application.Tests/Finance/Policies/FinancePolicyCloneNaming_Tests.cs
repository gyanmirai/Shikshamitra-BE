using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
	[Fact]
	public async Task Default_Proration_Clone_Skips_Archived_Name_Without_Changing_Default()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, _, _, head) = await SeedStandardAsync();
		var service = GetRequiredService<IProrationPolicyAppService>();
		var source = await service.CreateAsync(new()
		{
			AcademicYearId = year, PolicyName = "Default proration", Status = "Active",
			FeeHeadRules = [new() { FeeHeadId = head, RuleType = "Daily" }]
		});
		source.IsDefault.ShouldBeTrue();
		var first = await service.CloneAsync(source.Id, new());
		await service.ArchiveAsync(first.Id);
		var second = await service.CloneAsync(source.Id, new());
		second.PolicyName.ShouldBe("Default proration Copy 2");
		second.Status.ShouldBe("Draft");
		second.IsDefault.ShouldBeFalse();
		(await service.GetAsync(source.Id)).IsDefault.ShouldBeTrue();
	}

	[Fact]
	public async Task Refund_Clone_Skips_Archived_Name()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, _, _, _) = await SeedStandardAsync();
		var service = GetRequiredService<IRefundPolicyAppService>();
		var source = await service.CreateAsync(new() { AcademicYearId = year, PolicyName = "Refund policy" });
		var first = await service.CloneAsync(source.Id, new());
		await service.ArchiveAsync(first.Id);
		var second = await service.CloneAsync(source.Id, new());
		second.PolicyName.ShouldBe("Refund policy Copy 2");
		second.Status.ShouldBe("Draft");
		second.IsDefault.ShouldBeFalse();
	}

	[Fact]
	public async Task Collection_Clone_Skips_Archived_Name()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, _, _, head) = await SeedStandardAsync();
		var source = await CreateCollectionPolicyAsync(year, head, "Collection policy");
		var first = await _collectionPolicies.CloneAsync(source, new());
		await _collectionPolicies.ArchiveAsync(first.Id);
		var second = await _collectionPolicies.CloneAsync(source, new());
		second.PolicyName.ShouldBe("Collection policy Copy 2");
		second.Status.ShouldBe("Draft");
		second.IsDefault.ShouldBeFalse();
	}
}
