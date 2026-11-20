using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	// M08: a target's current placement is separate from each assessment's year.
	[Fact]
	public async Task Member_Charge_History_Should_Use_Allowed_Saved_Assessment_Years_Before_Count_And_Page()
	{
		var fixture = await SeedMemberChargeYearsAsync();
		var owner = fixture.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var denied = await RestrictMemberChargeReadAsync(owner);
		await AssertMemberChargeReadAuthorityAsync(owner, denied);
		await AssertMemberChargeTargetAsync(owner);
		await WithUnitOfWorkAsync(async () =>
		{
			var context = await GetRequiredService<ILibraryAccessService>().GetContextAsync();
			context.UserScope.HasTenantWideScope().ShouldBeFalse();
			context.AllowedAcademicYearIds.ShouldContain(owner.YearId);
			context.AllowedAcademicYearIds.ShouldNotContain(fixture.OtherYearId);
			var query = await ConsumerPositionService(owner).GetQueryAsync(new(LibraryChargePositionReadPurpose.MemberCharges));
			var rows = await GetRequiredService<IAsyncQueryableExecuter>().ToListAsync(query);
			rows.Count.ShouldBe(1); rows.Single().SourceId.ShouldBe(owner.AssessmentId);
			rows.Single().Evidence.ShouldBe(FinanceExternalPositionEvidence.Unposted);
		});

		var first = await WithUnitOfWorkAsync(() => MemberChargeReadService(owner).GetMemberChargesAsync(owner.PersonId, 1, 1));
		var empty = await WithUnitOfWorkAsync(() => MemberChargeReadService(owner).GetMemberChargesAsync(owner.PersonId, 2, 1));
		first.TotalCount.ShouldBe(1); first.Items.ShouldHaveSingleItem().Id.ShouldBe(owner.AssessmentId);
		empty.TotalCount.ShouldBe(1); empty.Items.ShouldBeEmpty();

		// Add only real year authority; retain the actual2026 target/stage grant.
		await AddMemberChargeYearAuthorityAsync(owner, fixture.OtherYearId);
		await AssertMemberChargeReadAuthorityAsync(owner, denied);
		await AssertMemberChargeTargetAsync(owner);
		await WithUnitOfWorkAsync(async () =>
		{
			var context = await GetRequiredService<ILibraryAccessService>().GetContextAsync();
			context.UserScope.HasTenantWideScope().ShouldBeFalse();
			context.AllowedAcademicYearIds.ShouldContain(owner.YearId);
			context.AllowedAcademicYearIds.ShouldContain(fixture.OtherYearId);
			var query = await ConsumerPositionService(owner).GetQueryAsync(new(LibraryChargePositionReadPurpose.MemberCharges));
			(await GetRequiredService<IAsyncQueryableExecuter>().CountAsync(query)).ShouldBe(2);
		});
		var expanded = await WithUnitOfWorkAsync(() => MemberChargeReadService(owner).GetMemberChargesAsync(owner.PersonId, 1, 10));
		expanded.TotalCount.ShouldBe(2); expanded.Items.Count.ShouldBe(2);
		expanded.Items.Select(row => row.Id).ShouldContain(owner.AssessmentId);
		expanded.Items.Select(row => row.Id).ShouldContain(fixture.OtherAssessmentId);
		expanded.Items.Select(row => row.Id).ShouldNotContain(fixture.UnknownAssessmentId);
	}
}

