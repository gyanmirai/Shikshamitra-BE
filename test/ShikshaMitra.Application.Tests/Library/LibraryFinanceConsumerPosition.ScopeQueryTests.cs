using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Library_Consumer_Position_Should_Use_Its_Own_Read_Action_And_Recorded_Source_Scope()
	{
		var moved = await SeedMovedTypedCashAsync();
		var owner = moved.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var deniedActions = await RestrictConsumerToDashboardAsync(owner);
		await SetMovedCashReaderScopeAsync(moved, currentPlacement: false);
		await AssertConsumerDashboardAuthorityAsync(moved, deniedActions, currentPlacement: false);

		await WithUnitOfWorkAsync(async () =>
		{
			var query = await ConsumerPositionService(owner).GetQueryAsync(new(
				LibraryChargePositionReadPurpose.Dashboard, owner.YearId));
			var row = await GetRequiredService<IAsyncQueryableExecuter>().SingleAsync(query);
			row.SourceId.ShouldBe(owner.AssessmentId);
			row.SourceLinkId.ShouldNotBeNull();
			row.AcademicYearId.ShouldBe(owner.YearId);
			row.Evidence.ShouldBe(FinanceExternalPositionEvidence.Verified);
			row.Settled.ShouldBe(40.25m);
			row.FinancialOutstanding.ShouldBe(60.50m);
			row.Collectible.ShouldBe(60.50m);
		});

		await SetMovedCashReaderScopeAsync(moved, currentPlacement: true);
		await AssertConsumerDashboardAuthorityAsync(moved, deniedActions, currentPlacement: true);
		await WithUnitOfWorkAsync(async () =>
		{
			var query = await ConsumerPositionService(owner).GetQueryAsync(new(
				LibraryChargePositionReadPurpose.Dashboard, owner.YearId));
			(await GetRequiredService<IAsyncQueryableExecuter>().CountAsync(query)).ShouldBe(0);
		});

		// Restore the readable recorded scope first: a missing owning grant must
		// deny, rather than returning the same zero produced by an excluded source.
		await SetMovedCashReaderScopeAsync(moved, currentPlacement: false);
		await SetConsumerGrantAsync(owner, ShikshaMitraSubmodulePermissions.Library.Dashboard.View, false);
		await WithUnitOfWorkAsync(async () =>
		{
			var checker = new FinanceServices(ServiceProvider, owner, useStoredSource: true)
				.GetRequiredService<IPermissionChecker>();
			(await checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Dashboard.View)).ShouldBeFalse();
			await Should.ThrowAsync<AbpAuthorizationException>(() => ConsumerPositionService(owner).GetQueryAsync(new(
				LibraryChargePositionReadPurpose.Dashboard, owner.YearId)));
		});
	}

	[Fact]
	public async Task Canonical_Consumer_Position_Should_Compose_Count_And_Pages_Without_Per_Source_Queries()
	{
		var first = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		var foreign = await SeedAsync(FinanceExternalPayerKind.Employee, departed: true);
		var owner = first.Settlement.Owner;
		foreign.TenantId.ShouldNotBe(owner.TenantId);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var one = await MeasureConsumerQueryAsync(owner);
		one.Total.ShouldBe(1);
		one.Actionable.ShouldBe(1);
		one.Page.ShouldBe([owner.AssessmentId]);

		var second = await SeedRefundSourceForSamePayerAsync(first);
		second.Settlement.Owner.PersonId.ShouldBe(owner.PersonId);
		second.Settlement.Owner.AssessmentId.ShouldNotBe(owner.AssessmentId);
		var form = await WithUnitOfWorkAsync(() => TypedService(second.Settlement.Owner)
			.GetCollectFormAsync(second.Settlement.Post.Source));
		var remainder = CashCollectionInput(second.Settlement, form.Revision);
		remainder.Payments.Single().Amount = 60.50m;
		var settled = await WithUnitOfWorkAsync(() => TypedService(second.Settlement.Owner).CollectAsync(remainder));
		settled.Receivable.FinancialOutstandingAmount.ShouldBe(0m);

		var two = await MeasureConsumerQueryAsync(owner);
		two.Total.ShouldBe(2);
		two.Actionable.ShouldBe(1);
		two.Page.ShouldBe([owner.AssessmentId]);
		two.SourceStatements.ShouldBe(one.SourceStatements);
		two.FinanceStatements.ShouldBe(one.FinanceStatements);

		// A genuine persisted foreign candidate is deliberately passed to Finance
		// with the TEST tenant filter disabled. The server tenant must intersect
		// candidates, rather than exposing that row as an Unposted position.
		FinanceExternalPositionSource foreignFacts;
		using (GetRequiredService<ICurrentTenant>().Change(foreign.TenantId))
		using (SignIn(foreign)) foreignFacts = await ConsumerRawSourceAsync(foreign.AssessmentId);
		await WithUnitOfWorkAsync(async () =>
		{
			using var filters = GetRequiredService<IDataFilter>().Disable<IMultiTenant>();
			var assessments = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetQueryableAsync();
			var candidates = ProjectForeignConsumerCandidate(assessments, foreignFacts);
			(await GetRequiredService<IAsyncQueryableExecuter>().CountAsync(candidates)).ShouldBe(1);
			var query = await GetRequiredService<IFinanceExternalPositionQuery>().GetQueryAsync(candidates);
			(await GetRequiredService<IAsyncQueryableExecuter>().CountAsync(query)).ShouldBe(0);
		});
	}
}
