using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Library;
using ShikshaMitra.Library.Books;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Operator_Post_Should_Reject_An_Employee_Student_Only_Annual_Cap_Before_Binding_The_Original_Source()
	{
		var owner = await SeedAsync(FinanceExternalPayerKind.Employee, departed: true);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		owner = await ConfirmStoredAssessmentAsync(owner);
		var incompatible = await SeedOperatorRefundPolicyAsync(owner, 100m, "Employee original cap");
		var compatible = await SeedOperatorRefundPolicyAsync(owner, null, "Employee original compatible");
		var input = Post(owner);
		input.RefundPolicyVersionId = incompatible;

		var error = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			Service(owner, useStoredSource: true).PostAsync(input)));

		error.Code.ShouldBe(LibraryFinanceErrorCodes.InvalidFinancePolicy);
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<Charge, Guid>>().CountAsync()).ShouldBe(0);
			(await GetRequiredService<IRepository<ChargeRequest, Guid>>().CountAsync()).ShouldBe(0);
			(await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>().CountAsync()).ShouldBe(0);
			(await GetRequiredService<IRepository<FinanceExternalReceivableCommand, Guid>>().CountAsync()).ShouldBe(0);
			var assessment = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(owner.AssessmentId);
			assessment.FinanceSourceLinkId.ShouldBeNull();
			assessment.GetAssessmentVersion().ShouldBe(owner.Version);
			assessment.Amount.ShouldBe(100m);
		});
		input.RefundPolicyVersionId = compatible;
		input.IdempotencyKey = Guid.NewGuid().ToString("N");
		var posted = await WithUnitOfWorkAsync(() => Service(owner, useStoredSource: true).PostAsync(input));
		posted.Source.SourceVersion.ShouldBe(owner.Version);
		posted.Payer.EmployeeId.ShouldBe(owner.PersonId);
		posted.Payer.StudentId.ShouldBeNull();
		posted.ApprovalStatus.ShouldBe(ChargeStatus.Pending);
		posted.OutstandingAmount.ShouldBe(0m);
		await WithUnitOfWorkAsync(async () =>
		{
			var link = (await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>()
				.GetListAsync(row => row.TenantId == owner.TenantId && row.SourceId == owner.AssessmentId)).Single();
			link.RefundPolicyVersionId.ShouldBe(compatible);
			link.SourceVersion.ShouldBe(owner.Version);
		});
	}

	[Fact]
	public async Task Operator_Post_Should_Retain_An_Explicit_Student_Annual_Cap_For_An_Actual_Student()
	{
		var owner = await SeedAsync(FinanceExternalPayerKind.Student, departed: true);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		owner = await ConfirmStoredAssessmentAsync(owner);
		var versionId = await SeedOperatorRefundPolicyAsync(owner, 100m, "Student original cap");
		var input = Post(owner);
		input.RefundPolicyVersionId = versionId;

		var posted = await WithUnitOfWorkAsync(() => Service(owner, useStoredSource: true).PostAsync(input));
		var replay = await WithUnitOfWorkAsync(() => Service(owner, useStoredSource: true).PostAsync(input));

		replay.FinanceChargeId.ShouldBe(posted.FinanceChargeId);
		posted.Payer.StudentId.ShouldBe(owner.PersonId);
		posted.Payer.EmployeeId.ShouldBeNull();
		posted.Source.SourceVersion.ShouldBe(owner.Version);
		posted.OriginalAmount.ShouldBe(100m);
		posted.ApprovalStatus.ShouldBe(ChargeStatus.Pending);
		posted.OutstandingAmount.ShouldBe(0m);
		await WithUnitOfWorkAsync(async () =>
		{
			var link = (await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>()
				.GetListAsync(row => row.TenantId == owner.TenantId && row.SourceId == owner.AssessmentId)).Single();
			link.RefundPolicyVersionId.ShouldBe(versionId);
			link.SourceVersion.ShouldBe(owner.Version);
			(await GetRequiredService<IRepository<FinanceExternalSettlementPolicy, Guid>>().CountAsync()).ShouldBe(0);
		});
	}

	[Fact]
	public async Task Operator_Binding_Should_Reject_An_Employee_Student_Only_Annual_Cap_Before_Creating_Immutable_Policy_Evidence()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Owner);
		var incompatible = await SeedOperatorRefundPolicyAsync(context, 100m, "Student cap");
		var compatible = await SeedOperatorRefundPolicyAsync(context, null, "Employee compatible");
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		form.SettlementPolicyBindingId.ShouldBeNull();
		var beforeCommands = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<FinanceExternalReceivableCommand, Guid>>().CountAsync());

		var denied = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => TypedService(context.Owner).BindPolicyAsync(new()
		{
			Source = context.Post.Source, Revision = form.Revision, CollectionPolicyVersionId = context.CollectionPolicyVersionId,
			RefundPolicyVersionId = incompatible, Reason = "A Student-only annual cap has no Employee meaning.", IdempotencyKey = Guid.NewGuid().ToString("N")
		})));

		denied.Code.ShouldBe(LibraryFinanceErrorCodes.InvalidFinancePolicy);
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<FinanceExternalSettlementPolicy, Guid>>().CountAsync()).ShouldBe(0);
			(await GetRequiredService<IRepository<FinanceExternalReceivableCommand, Guid>>().CountAsync()).ShouldBe(beforeCommands);
		});
		var reloaded = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		reloaded.Revision.ShouldBe(form.Revision);
		var bound = await WithUnitOfWorkAsync(() => TypedService(context.Owner).BindPolicyAsync(new()
		{
			Source = context.Post.Source, Revision = reloaded.Revision, CollectionPolicyVersionId = context.CollectionPolicyVersionId,
			RefundPolicyVersionId = compatible, Reason = "Explicitly selected a compatible Employee policy.", IdempotencyKey = Guid.NewGuid().ToString("N")
		}));
		bound.RefundPolicyVersionId.ShouldBe(compatible);
		await AssertOperatorOriginalPostPoliciesAsync(context);
	}

	[Fact]
	public async Task Operator_Binding_Should_Keep_The_Existing_Student_Annual_Cap_Valid_For_An_Actual_Student_Payer()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Student);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Owner);
		var versionId = await SeedOperatorRefundPolicyAsync(context, 100m, "Actual Student cap");
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		var bound = await WithUnitOfWorkAsync(() => TypedService(context.Owner).BindPolicyAsync(new()
		{
			Source = context.Post.Source, Revision = form.Revision, CollectionPolicyVersionId = context.CollectionPolicyVersionId,
			RefundPolicyVersionId = versionId, Reason = "Explicitly selected the actual Student's annual cap.", IdempotencyKey = Guid.NewGuid().ToString("N")
		}));
		bound.RefundPolicyVersionId.ShouldBe(versionId);
		bound.Receivable.Payer.Kind.ShouldBe(FinanceExternalPayerKind.Student);
		bound.Receivable.Payer.StudentId.ShouldBe(context.Owner.PersonId);
		bound.Receivable.Payer.EmployeeId.ShouldBeNull();
		bound.Receivable.ApprovalStatus.ShouldBe(ChargeStatus.Pending);
		bound.Receivable.OutstandingAmount.ShouldBe(0m);
		await AssertOperatorOriginalPostPoliciesAsync(context);
	}

	private Task<Guid> SeedOperatorRefundPolicyAsync(TypedSettlement context, decimal? annualCap, string suffix) =>
		SeedOperatorRefundPolicyAsync(context.Owner, annualCap, suffix);

	private Task<Guid> SeedOperatorRefundPolicyAsync(Fixture owner, decimal? annualCap, string suffix) => WithUnitOfWorkAsync(async () =>
	{
		var code = Guid.NewGuid().ToString("N")[..16];
		var policy = new RefundPolicy(Guid.NewGuid(), owner.TenantId, owner.YearId, new RefundPolicyUpdateModel(
			$"Operator {suffix}", code, null, RefundPolicyStatus.Active, false, RefundFeeHeadScope.AllFeeHeads,
			[], true, false, true, RefundAmountMode.Percentage, 50m, null, null, annualCap,
			RoundingMethodType.None, RefundDeductionType.None, null, null, null, [RefundPayoutMethod.Cash, RefundPayoutMethod.Upi], null));
		await GetRequiredService<IRepository<RefundPolicy, Guid>>().InsertAsync(policy, true);
		var version = new FinancePolicyVersion(Guid.NewGuid(), owner.TenantId, "refund", policy.Id, owner.YearId,
			policy.Version, policy.PolicyName, policy.Status.ToString(), JsonSerializer.Serialize(policy.ToSnapshot()), "created", "Operator compatibility control");
		await GetRequiredService<IRepository<FinancePolicyVersion, Guid>>().InsertAsync(version, true);
		return version.Id;
	});

	private Task AssertOperatorOriginalPostPoliciesAsync(TypedSettlement context) => WithUnitOfWorkAsync(async () =>
	{
		var link = (await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>()
			.GetListAsync(row => row.SourceId == context.Post.Source.SourceId)).Single();
		link.CollectionPolicyVersionId.ShouldBeNull(); link.RefundPolicyVersionId.ShouldBeNull();
		link.SourceVersion.ShouldBe(context.Post.Source.SourceVersion);
		(await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(context.Owner.AssessmentId))
			.GetAssessmentVersion().ShouldBe(context.Post.Source.SourceVersion);
	});
}
