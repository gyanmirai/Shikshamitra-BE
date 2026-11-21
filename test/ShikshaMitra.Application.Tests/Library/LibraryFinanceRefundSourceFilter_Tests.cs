using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Typed_Refund_Source_Filter_Should_Select_One_Of_Two_Real_Same_Payer_Sources_Before_Counting_And_Paging()
	{
		var first = await SeedTypedRefundAsync(FinanceExternalPayerKind.Student);
		var second = await SeedRefundSourceForSamePayerAsync(first);
		var owner = first.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var firstRequest = await SubmitRefundSourceFilterRequestAsync(first);
		var secondRequest = await SubmitRefundSourceFilterRequestAsync(second);
		var legacyDraftId = await SeedRefundSourceFilterLegacyDraftAsync(owner);
		await RestrictRefundSourceFilterReadActionsAsync(owner);
		await AssertRefundSourceFilterEvidenceAsync(first, firstRequest);
		await AssertRefundSourceFilterEvidenceAsync(second, secondRequest);
		var firstLinkId = await ReadRefundFilterLinkThroughLibraryAsync(first.Settlement.Owner);
		var secondLinkId = await ReadRefundFilterLinkThroughLibraryAsync(second.Settlement.Owner);
		firstLinkId.ShouldBe(firstRequest.FinanceExternalSourceLinkId!.Value);
		secondLinkId.ShouldBe(secondRequest.FinanceExternalSourceLinkId!.Value);
		second.Settlement.Owner.TenantId.ShouldBe(owner.TenantId);
		second.Settlement.Owner.PersonId.ShouldBe(owner.PersonId);
		secondRequest.AcademicYearId.ShouldBe(firstRequest.AcademicYearId);
		secondRequest.FinanceExternalSourceLinkId.ShouldNotBe(firstRequest.FinanceExternalSourceLinkId);

		var unfiltered = await WithUnitOfWorkAsync(() => DownstreamRefundService(owner).GetListAsync(new()
		{
			AcademicYearId = owner.YearId, WorkflowVersion = 1, MaxResultCount = 20
		}));
		unfiltered.TotalCount.ShouldBe(2);
		unfiltered.Items.Select(row => row.Id).OrderBy(id => id)
			.ShouldBe(new[] { firstRequest.Id, secondRequest.Id }.OrderBy(id => id));
		var allVisible = await WithUnitOfWorkAsync(() => DownstreamRefundService(owner).GetListAsync(new()
		{
			AcademicYearId = owner.YearId, MaxResultCount = 20
		}));
		allVisible.TotalCount.ShouldBe(3);
		allVisible.Items.Select(row => row.Id).OrderBy(id => id)
			.ShouldBe(new[] { firstRequest.Id, secondRequest.Id, legacyDraftId }.OrderBy(id => id));

		var page = await WithUnitOfWorkAsync(() => DownstreamRefundService(owner).GetListAsync(new()
		{
			AcademicYearId = owner.YearId, FinanceExternalSourceLinkId = firstLinkId,
			MaxResultCount = 1, SkipCount = 0
		}));
		var next = await WithUnitOfWorkAsync(() => DownstreamRefundService(owner).GetListAsync(new()
		{
			AcademicYearId = owner.YearId, FinanceExternalSourceLinkId = firstLinkId,
			MaxResultCount = 1, SkipCount = 1
		}));
		var otherSource = await WithUnitOfWorkAsync(() => DownstreamRefundService(owner).GetListAsync(new()
		{
			AcademicYearId = owner.YearId, FinanceExternalSourceLinkId = secondLinkId,
			MaxResultCount = 20
		}));

		page.TotalCount.ShouldBe(1);
		page.Items.Single().Id.ShouldBe(firstRequest.Id);
		page.Items.Single().StudentId.ShouldBe(owner.PersonId);
		page.Items.Single().EmployeeId.ShouldBeNull();
		page.Items.Single().FinanceExternalSourceLinkId.ShouldBe(firstRequest.FinanceExternalSourceLinkId);
		next.TotalCount.ShouldBe(1);
		next.Items.ShouldBeEmpty();
		otherSource.TotalCount.ShouldBe(1);
		otherSource.Items.Single().Id.ShouldBe(secondRequest.Id);
		var legacyOnly = await WithUnitOfWorkAsync(() => DownstreamRefundService(owner).GetListAsync(new()
		{
			AcademicYearId = owner.YearId, FinanceExternalSourceLinkId = firstLinkId,
			LegacyHistory = true, MaxResultCount = 1
		}));
		legacyOnly.TotalCount.ShouldBe(0);
		legacyOnly.Items.ShouldBeEmpty();
		await AssertRefundSourceFilterLegacyDraftUnchangedAsync(owner, legacyDraftId);
	}

	[Fact]
	public async Task Typed_Refund_Source_Filter_Should_Not_Fall_Back_To_Visible_Requests_For_Missing_Or_Foreign_Links()
	{
		var local = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		var foreign = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee);
		var owner = local.Settlement.Owner;
		Guid foreignLinkId;
		using (GetRequiredService<ICurrentTenant>().Change(foreign.Owner.TenantId))
		using (SignIn(foreign.Owner))
			foreignLinkId = await ReadRefundSourceFilterLinkIdAsync(foreign);
		foreign.Owner.TenantId.ShouldNotBe(owner.TenantId);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var request = await SubmitRefundSourceFilterRequestAsync(local);
		await RestrictRefundSourceFilterReadActionsAsync(owner);
		await AssertRefundSourceFilterEvidenceAsync(local, request);
		var before = await WithUnitOfWorkAsync(() => DownstreamRefundService(owner).GetListAsync(new()
		{
			AcademicYearId = owner.YearId, WorkflowVersion = 1, MaxResultCount = 20
		}));
		before.TotalCount.ShouldBe(1);
		before.Items.Single().Id.ShouldBe(request.Id);
		var selectedPositive = await WithUnitOfWorkAsync(() => DownstreamRefundService(owner).GetListAsync(new()
		{
			AcademicYearId = owner.YearId, FinanceExternalSourceLinkId = request.FinanceExternalSourceLinkId,
			WorkflowVersion = 1, MaxResultCount = 1
		}));
		selectedPositive.TotalCount.ShouldBe(1);
		selectedPositive.Items.Single().Id.ShouldBe(request.Id);
		selectedPositive.Items.Single().EmployeeId.ShouldBe(owner.PersonId);
		selectedPositive.Items.Single().StudentId.ShouldBeNull();

		foreach (var selectedLinkId in new[] { Guid.NewGuid(), foreignLinkId })
		{
			var result = await WithUnitOfWorkAsync(() => DownstreamRefundService(owner).GetListAsync(new()
			{
				AcademicYearId = owner.YearId, FinanceExternalSourceLinkId = selectedLinkId,
				WorkflowVersion = 1, MaxResultCount = 1, SkipCount = 0
			}));
			result.TotalCount.ShouldBe(0);
			result.Items.ShouldBeEmpty();
		}
		await AssertRefundSourceFilterEvidenceAsync(local, request);
	}

	[Fact]
	public async Task Typed_Refund_Source_Filter_Should_Intersect_An_Independently_Authorized_Billing_Year()
	{
		var source = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee,
			prepareSource: async owner => { await PrepareMovedCashLoanSourceAsync(owner); });
		var owner = source.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var request = await SubmitRefundSourceFilterRequestAsync(source);
		await RestrictRefundSourceFilterReadActionsAsync(owner);
		var otherYearId = await WithUnitOfWorkAsync(async () =>
		{
			var year = new AcademicYear(Guid.NewGuid(), owner.TenantId, "Other actual refund filter year",
				new DateTime(2025, 4, 1), new DateTime(2026, 3, 31));
			await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
			var scopes = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await scopes.DeleteAsync(row => row.TenantId == owner.TenantId && row.UserId == owner.ActorId, true);
			foreach (var yearId in new[] { owner.YearId, year.Id })
				await scopes.InsertAsync(new(Guid.NewGuid(), owner.TenantId, userId: owner.ActorId, academicYearId: yearId,
					description: "An independently authorized year for source-filter intersection"), true);
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(owner.ActorId);
			return year.Id;
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var services = new DownstreamRefundServices(new FinanceServices(ServiceProvider, owner, useStoredSource: true));
			await services.GetRequiredService<ShikshaMitra.Finance.FinanceScopeAccessService>()
				.EnsureCollectionYearAccessibleAsync(owner.YearId);
			await services.GetRequiredService<ShikshaMitra.Finance.FinanceScopeAccessService>()
				.EnsureCollectionYearAccessibleAsync(otherYearId);
			var readable = await services.GetRequiredService<IFinanceExternalSourceScope>()
				.GetReadableSourcesAsync(FinanceExternalSourceKind.LibraryAssessment);
			(await GetRequiredService<Volo.Abp.Linq.IAsyncQueryableExecuter>().AnyAsync(readable.Where(row =>
				row.SourceId == owner.AssessmentId && row.AcademicYearId == owner.YearId))).ShouldBeTrue(
				"The year intersection test retains an actual captured staff source; it does not grant an unassigned employee current HR access.");
		});
		var positive = await WithUnitOfWorkAsync(() => DownstreamRefundService(owner).GetListAsync(new()
		{
			AcademicYearId = owner.YearId, FinanceExternalSourceLinkId = request.FinanceExternalSourceLinkId,
			WorkflowVersion = 1, MaxResultCount = 1
		}));
		positive.TotalCount.ShouldBe(1);
		positive.Items.Single().Id.ShouldBe(request.Id);

		var mismatched = await WithUnitOfWorkAsync(() => DownstreamRefundService(owner).GetListAsync(new()
		{
			AcademicYearId = otherYearId, FinanceExternalSourceLinkId = request.FinanceExternalSourceLinkId,
			WorkflowVersion = 1, MaxResultCount = 1
		}));

		mismatched.TotalCount.ShouldBe(0);
		mismatched.Items.ShouldBeEmpty();
		await AssertRefundSourceFilterEvidenceAsync(source, request);
	}

	[Fact]
	public async Task Typed_Refund_Source_Filter_Should_Propagate_Revoked_Authority_And_Source_Query_Failures()
	{
		var source = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		var owner = source.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var request = await SubmitRefundSourceFilterRequestAsync(source);
		await RestrictRefundSourceFilterReadActionsAsync(owner);
		var input = new GetFeeRefundsInput
		{
			AcademicYearId = owner.YearId, FinanceExternalSourceLinkId = request.FinanceExternalSourceLinkId,
			WorkflowVersion = 1, MaxResultCount = 1
		};
		(await WithUnitOfWorkAsync(() => DownstreamRefundService(owner).GetListAsync(input))).TotalCount.ShouldBe(1);
		await SetRefundSourceFilterViewAsync(owner, false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() =>
			DownstreamRefundService(owner).GetListAsync(input)));
		await SetRefundSourceFilterViewAsync(owner, true);

		var fault = new InvalidOperationException("Deliberately unavailable canonical source query.");
		var failed = await Should.ThrowAsync<InvalidOperationException>(() => WithUnitOfWorkAsync(() =>
			RefundSourceFilterFailureService(owner, fault).GetListAsync(input)));
		failed.ShouldBeSameAs(fault);
		(await WithUnitOfWorkAsync(() => DownstreamRefundService(owner).GetListAsync(input))).TotalCount.ShouldBe(1);
		await AssertRefundSourceFilterEvidenceAsync(source, request);
	}
}
