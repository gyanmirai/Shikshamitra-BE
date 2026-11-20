using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Theory]
	[InlineData(FinanceExternalPayerKind.Student)]
	[InlineData(FinanceExternalPayerKind.Employee)]
	public async Task Typed_Source_Refund_Should_Preview_Submit_And_Replay_A_Partial_Refund_Without_A_Fee_Assignment(FinanceExternalPayerKind kind)
	{
		var fixture = await SeedTypedRefundAsync(kind);
		var context = fixture.Settlement;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Owner);
		var service = TypedRefundService(context.Owner);
		var policyVersionId = fixture.PolicyVersionId ?? throw new InvalidOperationException("This positive fixture requires its pinned refund policy version.");
		await WithUnitOfWorkAsync(async () =>
		{
			var version = await GetRequiredService<IRepository<FinancePolicyVersion, Guid>>().GetAsync(policyVersionId);
			JsonSerializer.Deserialize<RefundPolicySnapshot>(version.SnapshotJson)!.RoundingMethod.ShouldBe(RoundingMethodType.RoundUp);
		});
		var form = await WithUnitOfWorkAsync(() => service.GetFormAsync(context.Post.Source));
		form.CanRequest.ShouldBeTrue(string.Join(" ", form.SetupIssues));
		form.RefundPolicyVersionId.ShouldBe(fixture.PolicyVersionId);
		var source = form.Sources.Single(row => row.SourceAllocationId == fixture.AllocationId);
		source.PaidAmount.ShouldBe(40.25m);
		source.CommittedAmount.ShouldBe(0m);
		source.AvailableAmount.ShouldBe(40.25m);
		var input = RefundRequest(fixture, form.Revision, 15.25m);

		var preview = await WithUnitOfWorkAsync(() => service.PreviewAsync(input));
		var submitted = await WithUnitOfWorkAsync(() => service.CreateAsync(input));
		var replay = await WithUnitOfWorkAsync(() => service.CreateAsync(input));

		preview.RequestedAmount.ShouldBe(15.25m);
		preview.SettledPaidAmount.ShouldBe(40.25m);
		preview.MaximumAllowedAmount.ShouldBe(15m, "The recorded whole-rupee rule stays within the requested 15.25 ceiling.");
		preview.RefundPolicyVersionId.ShouldBe(policyVersionId);
		submitted.Status.ShouldBe(FeeRefundStatus.Submitted);
		submitted.TotalAmount.ShouldBe(15.25m);
		submitted.CalculatedAmount.ShouldBe(15m);
		submitted.ApprovedAmount.ShouldBe(0m);
		submitted.ProcessedAmount.ShouldBe(0m);
		submitted.StudentFeeStructureId.ShouldBeNull();
		submitted.FinanceFeeAccountId.ShouldBeNull();
		submitted.OriginalReceiptId.ShouldBe(fixture.Collection.ReceiptId);
		replay.Id.ShouldBe(submitted.Id);
		await WithUnitOfWorkAsync(async () =>
		{
			var refund = await GetRequiredService<IFeeRefundRepository>().FindWithLinesAsync(submitted.Id);
			refund.ShouldNotBeNull();
			AssertRecordedPayer(refund, kind, context.Owner.PersonId);
			refund.FinanceExternalSourceLinkId.ShouldNotBeNull();
			refund.RefundPolicyVersionId.ShouldBe(fixture.PolicyVersionId);
			refund.Lines.Single().SourceAllocationId.ShouldBe(fixture.AllocationId);
			refund.Lines.Single().RequestedAmount.ShouldBe(15.25m);
			refund.Lines.Single().CalculatedAmount.ShouldBe(15m);
			refund.Reservations.ShouldBeEmpty();
			refund.Settlements.ShouldBeEmpty();
			(await GetRequiredService<IRepository<FeeRefundRequest, Guid>>().CountAsync()).ShouldBe(1);
			(await GetRequiredService<IRepository<FeeRefundAccountingEntry, Guid>>().CountAsync()).ShouldBe(0);
			(await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().CountAsync()).ShouldBe(0);
		});
		var remaining = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		remaining.Receivable.SettledPaymentAmount.ShouldBe(40.25m);
		remaining.Receivable.OutstandingAmount.ShouldBe(60.50m);
		remaining.Receivable.RefundedAmount.ShouldBe(0m);
	}

	[Fact]
	public async Task Typed_Source_Refund_Should_Not_Treat_A_Pending_Bank_Submission_As_Refundable_Money()
	{
		var (context, collection) = await CollectTypedBankInstrumentAsync();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Owner);
		await GrantRefundOwnerActionsAsync(context);

		var form = await WithUnitOfWorkAsync(() => TypedRefundService(context.Owner).GetFormAsync(context.Post.Source));

		collection.Receivable.PendingVerificationAmount.ShouldBe(40.25m);
		form.CanRequest.ShouldBeFalse();
		form.Sources.ShouldBeEmpty();
		form.Receivable.SettledPaymentAmount.ShouldBe(0m);
		await AssertNoTypedRefundsAsync();
	}

	[Fact]
	public async Task Typed_Source_Refund_Should_Require_Its_Explicit_Pinned_Refund_Policy()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Student, includePolicy: false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.Settlement.Owner.TenantId);
		using var actor = SignIn(fixture.Settlement.Owner);

		var form = await WithUnitOfWorkAsync(() => TypedRefundService(fixture.Settlement.Owner).GetFormAsync(fixture.Settlement.Post.Source));

		form.RefundPolicyVersionId.ShouldBeNull();
		form.CanRequest.ShouldBeFalse();
		form.SetupIssues.ShouldContain(issue => issue.Contains("refund policy", StringComparison.OrdinalIgnoreCase));
		await AssertNoTypedRefundsAsync();
	}

	[Fact]
	public async Task Typed_Employee_Refund_Should_Report_An_Unsupported_Student_Only_Annual_Cap()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee, studentAnnualCap: 100m);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.Settlement.Owner.TenantId);
		using var actor = SignIn(fixture.Settlement.Owner);

		var form = await WithUnitOfWorkAsync(() => TypedRefundService(fixture.Settlement.Owner).GetFormAsync(fixture.Settlement.Post.Source));

		form.CanRequest.ShouldBeFalse();
		form.SetupIssues.ShouldContain(issue => issue.Contains("student", StringComparison.OrdinalIgnoreCase) &&
			issue.Contains("annual", StringComparison.OrdinalIgnoreCase));
		await AssertNoTypedRefundsAsync();
	}

	[Fact]
	public async Task Typed_Source_Refund_Should_Reject_An_Allocation_Amount_Above_Actually_Settled_Money()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.Settlement.Owner.TenantId);
		using var actor = SignIn(fixture.Settlement.Owner);
		var service = TypedRefundService(fixture.Settlement.Owner);
		var form = await WithUnitOfWorkAsync(() => service.GetFormAsync(fixture.Settlement.Post.Source));

		var error = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			service.CreateAsync(RefundRequest(fixture, form.Revision, 40.26m))));

		error.Code.ShouldBe(FeeRefundErrorCodes.InsufficientRefundBalance);
		await AssertNoTypedRefundsAsync();
	}

	[Fact]
	public async Task Typed_Source_Refund_Should_Recheck_A_Removed_Request_Action()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Student);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.Settlement.Owner.TenantId);
		using var actor = SignIn(fixture.Settlement.Owner);
		var service = TypedRefundService(fixture.Settlement.Owner);
		var form = await WithUnitOfWorkAsync(() => service.GetFormAsync(fixture.Settlement.Post.Source));
		await WithUnitOfWorkAsync(() => GetRequiredService<IPermissionManager>().SetAsync(
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add, "R", $"external-finance-{fixture.Settlement.Owner.ActorId:N}", false));

		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() =>
			service.CreateAsync(RefundRequest(fixture, form.Revision, 15.25m))));

		await AssertNoTypedRefundsAsync();
	}

	[Fact]
	public async Task Typed_Source_Refund_Should_Reject_A_Different_Receipt_Without_Changing_The_Original_Allocation()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.Settlement.Owner.TenantId);
		using var actor = SignIn(fixture.Settlement.Owner);
		var service = TypedRefundService(fixture.Settlement.Owner);
		var form = await WithUnitOfWorkAsync(() => service.GetFormAsync(fixture.Settlement.Post.Source));
		var input = RefundRequest(fixture, form.Revision, 15.25m);
		input.Lines[0].SourceReceiptId = Guid.NewGuid();

		var error = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => service.CreateAsync(input)));

		error.Code.ShouldBe(FeeRefundErrorCodes.SourceReceiptInvalid);
		await AssertNoTypedRefundsAsync();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>().GetAsync(fixture.AllocationId)))
			.Amount.ShouldBe(40.25m);
	}

	private IFinanceExternalRefundAppService TypedRefundService(Fixture fixture)
	{
		var services = new FinanceServices(ServiceProvider, fixture, useStoredSource: true);
		var authority = ActivatorUtilities.CreateInstance<FinanceExternalSettlementAuthority>(services);
		var service = ActivatorUtilities.CreateInstance<FinanceExternalRefundAppService>(services, authority);
		service.LazyServiceProvider = new AbpLazyServiceProvider(services);
		return service;
	}

	private async Task<TypedRefundFixture> SeedTypedRefundAsync(FinanceExternalPayerKind kind,
		bool includePolicy = true, decimal? studentAnnualCap = null, Func<Fixture, Task>? prepareSource = null)
	{
		var context = await SeedTypedSettlementAsync(kind, prepareSource: prepareSource);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Owner);
		await GrantRefundOwnerActionsAsync(context);
		var refundVersionId = await WithUnitOfWorkAsync<Guid?>(async () =>
		{
			if (!includePolicy) return null;
			var policy = new RefundPolicy(Guid.NewGuid(), context.Owner.TenantId, context.Owner.YearId, new RefundPolicyUpdateModel(
				"Explicit source refund", "LIBRARY-REFUND", null, RefundPolicyStatus.Active, false, RefundFeeHeadScope.AllFeeHeads,
				[], true, false, true, RefundAmountMode.Percentage, 50m, null, null, studentAnnualCap,
				RoundingMethodType.None, RefundDeductionType.None, null, null, null, [RefundPayoutMethod.Cash, RefundPayoutMethod.Upi], null));
			await GetRequiredService<IRepository<RefundPolicy, Guid>>().InsertAsync(policy, true);
			var version = new FinancePolicyVersion(Guid.NewGuid(), context.Owner.TenantId, "refund", policy.Id, context.Owner.YearId,
				policy.Version, policy.PolicyName, policy.Status.ToString(), JsonSerializer.Serialize(policy.ToSnapshot()), "created", "Reviewed source refund rules");
			await GetRequiredService<IRepository<FinancePolicyVersion, Guid>>().InsertAsync(version, true);
			return version.Id;
		});
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		if (kind == FinanceExternalPayerKind.Employee && studentAnnualCap.HasValue)
		{
			// Existing incompatible history must remain readable as blocked. New
			// Post/Bind commands reject this state; do not use them to manufacture it.
			await WithUnitOfWorkAsync(async () =>
			{
				var link = (await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>()
					.GetListAsync(row => row.TenantId == context.Owner.TenantId && row.SourceId == context.Owner.AssessmentId)).Single();
				await GetRequiredService<IRepository<FinanceExternalSettlementPolicy, Guid>>().InsertAsync(new(
					Guid.NewGuid(), context.Owner.TenantId, link.Id, context.CollectionPolicyVersionId,
					refundVersionId, context.Owner.ActorId, "Historical test evidence: Student-only cap was bound before the creation guard."), true);
			});
		}
		else
			await WithUnitOfWorkAsync(() => TypedService(context.Owner).BindPolicyAsync(new()
			{
				Source = context.Post.Source, Revision = form.Revision, CollectionPolicyVersionId = context.CollectionPolicyVersionId,
				RefundPolicyVersionId = refundVersionId, Reason = "Confirmed collection and refund policies for this source.",
				IdempotencyKey = Guid.NewGuid().ToString("N")
			}));
		using (SignIn(context.Reviewer))
		{
			form = await WithUnitOfWorkAsync(() => TypedService(context.Reviewer).GetCollectFormAsync(context.Post.Source));
			await WithUnitOfWorkAsync(() => TypedService(context.Reviewer).ApproveAsync(new()
			{
				Source = context.Post.Source, Revision = form.Revision, Reason = "Reviewed the original source assessment.",
				IdempotencyKey = Guid.NewGuid().ToString("N")
			}));
		}
		form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		var collection = await WithUnitOfWorkAsync(() => TypedService(context.Owner).CollectAsync(CashCollectionInput(context, form.Revision)));
		var allocation = await WithUnitOfWorkAsync(async () => (await GetRequiredService<IRepository<ChargeCollectionRecord, Guid>>()
			.GetListAsync(row => row.FeeRecordMetadataId == collection.CollectionId)).Single());
		return new(context, collection, allocation.Id, refundVersionId);
	}

	private Task GrantRefundOwnerActionsAsync(TypedSettlement context) => WithUnitOfWorkAsync(async () =>
	{
		foreach (var action in new[] { ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View, ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add })
			await GetRequiredService<IPermissionManager>().SetAsync(action, "R", $"external-finance-{context.Owner.ActorId:N}", true);
	});

	private static FinanceExternalRefundRequestInput RefundRequest(TypedRefundFixture fixture, string revision, decimal amount) => new()
	{
		Source = fixture.Settlement.Post.Source, Revision = revision, OriginalReceiptId = fixture.Collection.ReceiptId,
		Method = "UPI", Reason = "Requested a reviewed partial refund of the actual settled recovery payment.",
		IdempotencyKey = Guid.NewGuid().ToString("N"),
		Lines = [new() { SourceAllocationId = fixture.AllocationId, SourceReceiptId = fixture.Collection.ReceiptId, Amount = amount }]
	};

	private Task AssertNoTypedRefundsAsync() => WithUnitOfWorkAsync(async () =>
	{
		(await GetRequiredService<IRepository<FeeRefundRequest, Guid>>().CountAsync()).ShouldBe(0);
		(await GetRequiredService<IAsyncQueryableExecuter>().CountAsync(
			(await GetRequiredService<IFeeRefundRepository>().GetQueryableAsync()).SelectMany(request => request.Reservations))).ShouldBe(0);
		(await GetRequiredService<IRepository<FeeRefundAccountingEntry, Guid>>().CountAsync()).ShouldBe(0);
	});

	private sealed record TypedRefundFixture(TypedSettlement Settlement, FinanceExternalCollectionDto Collection,
		Guid AllocationId, Guid? PolicyVersionId);
}
