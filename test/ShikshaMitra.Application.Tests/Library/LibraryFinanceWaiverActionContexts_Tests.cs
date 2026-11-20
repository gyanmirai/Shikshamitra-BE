using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.ExternalReceivables.Operator;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Theory]
	[InlineData(FinanceExternalWaiverAction.Apply)]
	[InlineData(FinanceExternalWaiverAction.Approve)]
	[InlineData(FinanceExternalWaiverAction.Reject)]
	[InlineData(FinanceExternalWaiverAction.Reverse)]
	public async Task Operator_Waiver_Context_Should_Enable_Only_Its_Own_Action_With_Recorded_Source_Authority(FinanceExternalWaiverAction action)
	{
		// A real second school's pending request makes the count/page tenant fence causal.
		WaiverReadFixture? foreign = action == FinanceExternalWaiverAction.Approve
			? await SeedWaiverReadFixtureAsync(FinanceExternalWaiverAction.Approve) : null;
		var fixture = await SeedWaiverReadFixtureAsync(action);
		var owner = fixture.Settlement.Owner;
		var actor = action == FinanceExternalWaiverAction.Apply ? owner : fixture.Settlement.Reviewer;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var identity = SignIn(actor);
		await RestrictWaiverReadActionsAsync(fixture, actor, action);
		await SetWaiverReadPlacementAsync(fixture, actor, currentPlacement: false);
		await AssertWaiverReadPlacementAsync(fixture, currentPlacement: false);

		var context = await WithUnitOfWorkAsync(() => OperatorService(actor).GetWaiverContextAsync(fixture.Settlement.Post.Source, action, 1, 1));
		var next = await WithUnitOfWorkAsync(() => OperatorService(actor).GetWaiverContextAsync(fixture.Settlement.Post.Source, action, 2, 1));

		context.Source.SourceId.ShouldBe(owner.AssessmentId);
		context.Source.SourceVersion.ShouldBe(owner.Version);
		context.Receivable.Payer.EmployeeId.ShouldBe(owner.PersonId);
		context.Receivable.AcademicYearId.ShouldBe(owner.YearId);
		context.CollectionPolicyVersionId.ShouldBe(fixture.Settlement.CollectionPolicyVersionId);
		context.WaiverEnabled.ShouldBeTrue();
		context.RequiresApproval.ShouldBeTrue();
		context.Revision.ShouldNotBeNullOrWhiteSpace();
		context.Revision.ShouldNotBe(owner.Version);
		context.CanExecute.ShouldBeTrue(string.Join(" ", context.Issues));
		context.Requests.TotalCount.ShouldBe(fixture.Waiver == null ? 0 : 1);
		next.Requests.TotalCount.ShouldBe(context.Requests.TotalCount);
		next.Requests.Items.ShouldBeEmpty();
		if (fixture.Waiver != null)
		{
			var row = context.Requests.Items.Single();
			row.RequestId.ShouldBe(fixture.Waiver.RequestId);
			row.ApplicationId.ShouldBe(fixture.Waiver.ApplicationId);
			row.State.ShouldBe(fixture.Waiver.State);
			row.RequestedAmount.ShouldBe(10.10m);
			row.Reason.ShouldBe("Requested school assistance.");
			row.CanAct.ShouldBeTrue(string.Join(" ", row.Issues));
			if (foreign != null) row.RequestId.ShouldNotBe(foreign.Waiver!.RequestId);
			if (action == FinanceExternalWaiverAction.Reverse) row.ReversibleAmount.ShouldBe(10.10m);
		}
		await ExecuteOwnWaiverContextAsync(fixture, actor, action, context);
		await AssertNoTypedCollectionsAsync();
	}

	[Fact]
	public async Task Operator_Waiver_Context_Should_Keep_Makers_Pending_Evidence_Visible_While_Blocking_Their_Decision()
	{
		var fixture = await SeedWaiverReadFixtureAsync(FinanceExternalWaiverAction.Approve);
		var owner = fixture.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var identity = SignIn(owner);
		await RestrictWaiverReadActionsAsync(fixture, owner, FinanceExternalWaiverAction.Approve);
		var context = await WithUnitOfWorkAsync(() => OperatorService(owner).GetWaiverContextAsync(fixture.Settlement.Post.Source, FinanceExternalWaiverAction.Approve));

		context.CanExecute.ShouldBeFalse();
		context.Requests.TotalCount.ShouldBe(1);
		var row = context.Requests.Items.Single();
		row.RequestId.ShouldBe(fixture.Waiver!.RequestId);
		row.CanAct.ShouldBeFalse();
		row.Issues.ShouldContain(issue => issue.Contains("independent", StringComparison.OrdinalIgnoreCase));
		var error = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => TypedService(owner).ApproveWaiverAsync(new()
		{
			Source = context.Source, Revision = context.Revision, WaiverRequestId = row.RequestId,
			Reason = "The original maker cannot decide their own request.", IdempotencyKey = Guid.NewGuid().ToString("N")
		})));
		error.Code.ShouldBe(LibraryFinanceErrorCodes.IndependentApprovalRequired);
		await AssertWaiverStillPendingAsync(fixture);
	}

	[Fact]
	public async Task Operator_Waiver_Context_Should_Recheck_Revoked_Action_And_Saved_Billing_Year_Before_Discovery()
	{
		var fixture = await SeedWaiverReadFixtureAsync(FinanceExternalWaiverAction.Approve);
		var reviewer = fixture.Settlement.Reviewer;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(reviewer.TenantId);
		using var identity = SignIn(reviewer);
		await RestrictWaiverReadActionsAsync(fixture, reviewer, FinanceExternalWaiverAction.Approve);
		var original = await WithUnitOfWorkAsync(() => OperatorService(reviewer).GetWaiverContextAsync(fixture.Settlement.Post.Source, FinanceExternalWaiverAction.Approve));
		original.Requests.TotalCount.ShouldBe(1);
		await WithUnitOfWorkAsync(() => GetRequiredService<IPermissionManager>().SetAsync(WaiverReadAction(FinanceExternalWaiverAction.Approve),
			"R", WaiverReadRole(fixture, reviewer), false));
		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() =>
			OperatorService(reviewer).GetWaiverContextAsync(fixture.Settlement.Post.Source, FinanceExternalWaiverAction.Approve)));
		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() => TypedService(reviewer).ApproveWaiverAsync(new()
		{
			Source = original.Source, Revision = original.Revision, WaiverRequestId = fixture.Waiver!.RequestId,
			Reason = "An old context cannot restore a revoked action.", IdempotencyKey = Guid.NewGuid().ToString("N")
		})));
		await WithUnitOfWorkAsync(() => GetRequiredService<IPermissionManager>().SetAsync(WaiverReadAction(FinanceExternalWaiverAction.Approve),
			"R", WaiverReadRole(fixture, reviewer), true));
		await GiveWaiverReaderAnotherYearAsync(reviewer);
		var denied = await Record.ExceptionAsync(() => WithUnitOfWorkAsync(() =>
			OperatorService(reviewer).GetWaiverContextAsync(fixture.Settlement.Post.Source, FinanceExternalWaiverAction.Approve)));
		AssertWaiverContextDenied(denied);
		await AssertWaiverStillPendingAsync(fixture);
	}

	[Fact]
	public async Task Operator_Waiver_Context_Should_Deny_Current_Hr_Only_And_Foreign_Tenant_Sources_Before_Counting()
	{
		var foreign = await SeedWaiverReadFixtureAsync(FinanceExternalWaiverAction.Approve);
		var fixture = await SeedWaiverReadFixtureAsync(FinanceExternalWaiverAction.Approve);
		var reviewer = fixture.Settlement.Reviewer;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(reviewer.TenantId);
		using var identity = SignIn(reviewer);
		await RestrictWaiverReadActionsAsync(fixture, reviewer, FinanceExternalWaiverAction.Approve);
		await SetWaiverReadPlacementAsync(fixture, reviewer, currentPlacement: true);
		await AssertWaiverReadPlacementAsync(fixture, currentPlacement: true);
		var currentOnly = await Record.ExceptionAsync(() => WithUnitOfWorkAsync(() =>
			OperatorService(reviewer).GetWaiverContextAsync(fixture.Settlement.Post.Source, FinanceExternalWaiverAction.Approve)));
		AssertWaiverContextDenied(currentOnly);
		var otherSchool = await Record.ExceptionAsync(() => WithUnitOfWorkAsync(() =>
			OperatorService(reviewer).GetWaiverContextAsync(foreign.Settlement.Post.Source, FinanceExternalWaiverAction.Approve)));
		AssertWaiverContextDenied(otherSchool);
		await AssertWaiverStillPendingAsync(fixture);
	}

	[Fact]
	public async Task Typed_Refund_Form_Should_Expose_Actual_Pinned_Methods_Without_Collection_Or_Settings_Grants()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		var owner = fixture.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var identity = SignIn(owner);
		await RestrictWaiverReadActionsAsync(new(fixture.Settlement, null, null), owner, null,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View);
		var pinnedId = fixture.PolicyVersionId!.Value;
		var savedJson = await WithUnitOfWorkAsync(async () =>
		{
			var version = await GetRequiredService<IRepository<FinancePolicyVersion, Guid>>().GetAsync(pinnedId);
			var snapshot = JsonSerializer.Deserialize<RefundPolicySnapshot>(version.SnapshotJson)!;
			snapshot.AllowedPayoutMethods.OrderBy(value => value).ShouldBe(new[] { RefundPayoutMethod.Cash, RefundPayoutMethod.Upi }.OrderBy(value => value));
			var parent = await GetRequiredService<IRepository<RefundPolicy, Guid>>().GetAsync(version.PolicyId);
			parent.Update(RefundMethodDriftModel(snapshot, parent.Description));
			await GetRequiredService<IRepository<RefundPolicy, Guid>>().UpdateAsync(parent, true);
			JsonSerializer.Deserialize<RefundPayoutMethod[]>(parent.AllowedPayoutMethodsJson)!.ShouldBe(new[] { RefundPayoutMethod.BankTransfer });
			return version.SnapshotJson;
		});

		var form = await WithUnitOfWorkAsync(() => TypedRefundService(owner).GetFormAsync(fixture.Settlement.Post.Source));

		form.CanRequest.ShouldBeTrue(string.Join(" ", form.SetupIssues));
		form.RefundPolicyVersionId.ShouldBe(pinnedId);
		form.AllowedPayoutMethods.OrderBy(value => value).ShouldBe(new[] { "Cash", "UPI" }.OrderBy(value => value));
		form.Sources.Single().SourceAllocationId.ShouldBe(fixture.AllocationId);
		form.Sources.Single().AvailableAmount.ShouldBe(40.25m);
		form.Receivable.Payer.EmployeeId.ShouldBe(owner.PersonId);
		form.Receivable.Payer.StudentId.ShouldBeNull();
		await WithUnitOfWorkAsync(async () => (await GetRequiredService<IRepository<FinancePolicyVersion, Guid>>()
			.GetAsync(pinnedId)).SnapshotJson.ShouldBe(savedJson));
		await AssertNoTypedRefundsAsync();
	}
}
