using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Member_Charge_Pages_Should_Select_Exactly_The_Requested_Person_And_Their_Historical_Aliases()
	{
		var foreign = await SeedAsync(FinanceExternalPayerKind.Employee, departed: false);
		var owner = await SeedAsync(FinanceExternalPayerKind.Student, departed: false);
		foreign.TenantId.ShouldNotBe(owner.TenantId);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var fixture = await SeedMemberChargeAliasesAsync(owner);
		var denied = await RestrictMemberChargeReadAsync(owner);
		await AssertMemberChargeReadAuthorityAsync(owner, denied);
		await AssertMemberChargeTargetAsync(owner);
		await AssertMemberChargeTargetAsync(owner, fixture.Aliases[0]);
		await AssertMemberChargeTargetAsync(owner, fixture.Aliases[1]);
		await WithUnitOfWorkAsync(async () =>
		{
			var access = GetRequiredService<ILibraryAccessService>();
			(await access.EnsureMemberAsync(fixture.OtherPersonId)).ShouldBe(fixture.OtherPersonId);
			(await access.GetPersonMemberIdsAsync(owner.PersonId)).OrderBy(id => id).ToArray()
				.ShouldBe(fixture.Aliases.Append(owner.PersonId).OrderBy(id => id).ToArray());
			var query = await ConsumerPositionService(owner).GetQueryAsync(new(LibraryChargePositionReadPurpose.MemberCharges,
				AssessmentIds: fixture.ExpectedOrder));
			(await GetRequiredService<IAsyncQueryableExecuter>().CountAsync(query)).ShouldBe(3);
		});

		var service = MemberChargeReadService(owner);
		var first = await WithUnitOfWorkAsync(() => service.GetMemberChargesAsync(owner.PersonId, 1, 2));
		var second = await WithUnitOfWorkAsync(() => service.GetMemberChargesAsync(owner.PersonId, 2, 2));
		var aliasFirst = await WithUnitOfWorkAsync(() => service.GetMemberChargesAsync(fixture.Aliases[0], 1, 2));
		var inactiveAlias = await WithUnitOfWorkAsync(() => service.GetMemberChargesAsync(fixture.Aliases[1], 1, 100));

		first.TotalCount.ShouldBe(3); second.TotalCount.ShouldBe(3);
		first.TotalPages.ShouldBe(2); first.CurrentPage.ShouldBe(1); second.CurrentPage.ShouldBe(2);
		first.Items.Select(row => row.Id).ShouldBe(fixture.ExpectedOrder.Take(2));
		second.Items.Select(row => row.Id).ShouldBe(fixture.ExpectedOrder.Skip(2));
		aliasFirst.TotalCount.ShouldBe(3);
		aliasFirst.Items.Select(row => row.Id).ShouldBe(first.Items.Select(row => row.Id));
		inactiveAlias.TotalCount.ShouldBe(3);
		inactiveAlias.Items.Select(row => row.Id).ShouldBe(fixture.ExpectedOrder);
		var all = first.Items.Concat(second.Items).ToArray();
		all.Select(row => row.Id).Distinct().Count().ShouldBe(3);
		all.ShouldNotContain(row => row.Id == fixture.OtherAssessmentId || row.Id == foreign.AssessmentId);
		foreach (var row in all)
		{
			var position = RequireMemberChargePosition(row);
			position.GetProperty("evidence").GetInt32().ShouldBe((int)FinanceExternalPositionEvidence.Unposted);
			position.GetProperty("amounts").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
		}
		await AssertNoTypedCollectionsAsync();
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>().CountAsync()).ShouldBe(0));
	}
}
