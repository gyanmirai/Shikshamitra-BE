using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Member_Charge_Extreme_Page_Should_Not_Overflow_Or_Hydrate_An_Empty_Page()
	{
		var owner = await SeedAsync(FinanceExternalPayerKind.Employee, departed: false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var denied = await RestrictMemberChargeReadAsync(owner);
		await AssertMemberChargeReadAuthorityAsync(owner, denied);
		await AssertMemberChargeTargetAsync(owner);
		(await MemberChargePositionAsync(owner)).Evidence.ShouldBe(FinanceExternalPositionEvidence.Unposted);
		var details = new RecordingMemberChargeDetails(GetRequiredService<IFinanceExternalPositionReader>(), rejectCalls: true);

		var page = await WithUnitOfWorkAsync(() => MemberChargeReadService(owner, details)
			.GetMemberChargesAsync(owner.PersonId, int.MaxValue, 100));

		page.TotalCount.ShouldBe(1); page.TotalPages.ShouldBe(1);
		page.CurrentPage.ShouldBe(int.MaxValue); page.PageSize.ShouldBe(100);
		page.Items.ShouldBeEmpty("A positive long offset must never wrap back to page one.");
		details.Calls.ShouldBeEmpty("Empty pages must not invoke the bounded Finance reader.");
		await AssertNoTypedCollectionsAsync();
	}

	[Fact]
	public async Task Member_Charge_Page_Hydration_Should_Be_Bounded_And_Use_Fixed_Bulk_Finance_Queries()
	{
		var first = await SeedTypedRefundAsync(FinanceExternalPayerKind.Student);
		var owner = first.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var sources = new System.Collections.Generic.List<Guid> { owner.AssessmentId };
		for (var index = 0; index < 20; index++)
		{
			var next = await SeedRefundSourceForSamePayerAsync(first);
			next.Collection.Receivable.SettledPaymentAmount.ShouldBe(40.25m);
			sources.Add(next.Settlement.Owner.AssessmentId);
		}
		var unrelated = await SeedOtherMemberChargeStudentAsync(owner);
		var denied = await RestrictMemberChargeReadAsync(owner);
		await AssertMemberChargeReadAuthorityAsync(owner, denied);
		await AssertMemberChargeTargetAsync(owner);
		await AssertMemberChargeBulkSourcesAsync(owner, sources.ToArray(), unrelated);
		var details = new RecordingMemberChargeDetails(GetRequiredService<IFinanceExternalPositionReader>());

		var one = await MeasureMemberChargePageAsync(owner, 1, details);
		var twenty = await MeasureMemberChargePageAsync(owner, 20, details);

		one.Page.TotalCount.ShouldBe(21); twenty.Page.TotalCount.ShouldBe(21);
		one.Page.Items.Count.ShouldBe(1); twenty.Page.Items.Count.ShouldBe(20);
		twenty.Page.Items.Select(row => row.Id).Distinct().Count().ShouldBe(20);
		details.Calls.Count.ShouldBe(2, "Exactly one bounded Finance detail read is permitted for each nonempty page.");
		details.Calls[0].OrderBy(id => id).ShouldBe(one.Page.Items.Select(row => row.Id).OrderBy(id => id));
		details.Calls[1].OrderBy(id => id).ShouldBe(twenty.Page.Items.Select(row => row.Id).OrderBy(id => id));
		details.Calls.ShouldAllBe(ids => ids.Length <= 100 && ids.All(sources.Contains));
		details.Calls[1].Length.ShouldBeLessThan(sources.Count);
		one.FinanceStatements.ShouldBeGreaterThan(0);
		twenty.FinanceStatements.ShouldBe(one.FinanceStatements, "Finance I/O must not grow with page length.");
		one.SourceStatements.ShouldBeGreaterThan(0);
		twenty.SourceStatements.ShouldBe(one.SourceStatements, "The owning source/page reads must remain fixed in number.");
		one.HasActualAllocationQuery.ShouldBeTrue(); twenty.HasActualAllocationQuery.ShouldBeTrue();
		foreach (var row in twenty.Page.Items)
		{
			var position = RequireMemberChargePosition(row);
			position.GetProperty("evidence").GetInt32().ShouldBe((int)FinanceExternalPositionEvidence.Verified);
			position.GetProperty("amounts").GetProperty("settledAmountText").GetString().ShouldBe("40.25");
		}
	}
}
