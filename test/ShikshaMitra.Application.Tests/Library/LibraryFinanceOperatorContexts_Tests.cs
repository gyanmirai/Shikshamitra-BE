using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.ExternalReceivables.Operator;
using ShikshaMitra.Finance.Fees.Charges;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Operator_Approval_Context_Should_Supply_A_Command_Revision_Without_Collection_View()
	{
		var fixture = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.Owner.TenantId);
		using var actor = SignIn(fixture.Reviewer);
		await RevokeOperatorCollectionViewAsync(fixture.Reviewer, $"source-reviewer-{fixture.Reviewer.ActorId:N}");
		(await OperatorGrantedAsync(fixture.Reviewer,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve)).ShouldBeTrue();

		var context = await WithUnitOfWorkAsync(() => OperatorService(fixture.Reviewer)
			.GetApprovalContextAsync(fixture.Post.Source));

		AssertOperatorSource(context, fixture);
		context.Receivable.ApprovalStatus.ShouldBe(ChargeStatus.Pending);
		context.Revision.ShouldNotBeNullOrWhiteSpace();
		context.CanExecute.ShouldBeTrue();
		context.Issues.ShouldBeEmpty();
		var approved = await WithUnitOfWorkAsync(() => TypedService(fixture.Reviewer).ApproveAsync(new()
		{
			Source = context.Source,
			Revision = context.Revision,
			Reason = "Reviewed the recorded Library assessment independently.",
			IdempotencyKey = Guid.NewGuid().ToString("N")
		}));

		approved.ApprovalStatus.ShouldBe(ChargeStatus.Approved);
		approved.Source.SourceVersion.ShouldBe(fixture.Owner.Version);
		await AssertNoTypedCollectionsAsync();
	}

	[Fact]
	public async Task Operator_Approval_Context_Should_Explain_Independent_Review_Without_Allowing_Maker_Approval()
	{
		var fixture = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Student);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.Owner.TenantId);
		using var actor = SignIn(fixture.Owner);
		await RevokeOperatorCollectionViewAsync(fixture.Owner, $"external-finance-{fixture.Owner.ActorId:N}");
		(await OperatorGrantedAsync(fixture.Owner,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve)).ShouldBeTrue();

		var context = await WithUnitOfWorkAsync(() => OperatorService(fixture.Owner)
			.GetApprovalContextAsync(fixture.Post.Source));

		AssertOperatorSource(context, fixture);
		context.Revision.ShouldNotBeNullOrWhiteSpace();
		context.CanExecute.ShouldBeFalse();
		context.Issues.ShouldContain(issue => issue.Contains("independent", StringComparison.OrdinalIgnoreCase));
		var error = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			TypedService(fixture.Owner).ApproveAsync(new()
			{
				Source = context.Source,
				Revision = context.Revision,
				Reason = "Attempted approval by the original assessment maker.",
				IdempotencyKey = Guid.NewGuid().ToString("N")
			})));

		error.Code.ShouldBe(LibraryFinanceErrorCodes.IndependentApprovalRequired);
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<Charge, Guid>>().GetAsync(fixture.Posted.FinanceChargeId))
				.Status.ShouldBe(ChargeStatus.Pending));
		await AssertNoTypedCollectionsAsync();
	}

	[Fact]
	public async Task Operator_Binding_Context_Should_Accept_An_Actual_Policy_Version_Without_Collection_View()
	{
		var fixture = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.Owner.TenantId);
		using var actor = SignIn(fixture.Owner);
		await RevokeOperatorCollectionViewAsync(fixture.Owner, $"external-finance-{fixture.Owner.ActorId:N}");
		(await OperatorGrantedAsync(fixture.Owner,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update)).ShouldBeTrue();
		await WithUnitOfWorkAsync(async () =>
		{
			var version = await GetRequiredService<IRepository<FinancePolicyVersion, Guid>>()
				.GetAsync(fixture.CollectionPolicyVersionId);
			version.PolicyId.ShouldNotBe(version.Id);
			version.AcademicYearId.ShouldBe(fixture.Owner.YearId);
			version.PolicyType.ShouldBe("collection");
		});

		var context = await WithUnitOfWorkAsync(() => OperatorService(fixture.Owner)
			.GetBindingContextAsync(fixture.Post.Source));

		AssertOperatorSource(context, fixture);
		context.Revision.ShouldNotBeNullOrWhiteSpace();
		context.SettlementPolicyBindingId.ShouldBeNull();
		context.CollectionPolicyVersionId.ShouldBeNull();
		context.RefundPolicyVersionId.ShouldBeNull();
		context.CanExecute.ShouldBeTrue();
		var bound = await WithUnitOfWorkAsync(() => TypedService(fixture.Owner).BindPolicyAsync(new()
		{
			Source = context.Source,
			Revision = context.Revision,
			CollectionPolicyVersionId = fixture.CollectionPolicyVersionId,
			Reason = "Explicitly selected the immutable school recovery policy version.",
			IdempotencyKey = Guid.NewGuid().ToString("N")
		}));

		bound.SettlementPolicyBindingId.ShouldNotBeNull();
		bound.CollectionPolicyVersionId.ShouldBe(fixture.CollectionPolicyVersionId);
		bound.Receivable.Source.SourceVersion.ShouldBe(fixture.Owner.Version);
		await WithUnitOfWorkAsync(async () =>
		{
			var link = (await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>()
				.GetListAsync(row => row.TenantId == fixture.Owner.TenantId && row.SourceId == fixture.Owner.AssessmentId))
				.Single();
			link.SourceVersion.ShouldBe(fixture.Owner.Version);
			link.CollectionPolicyVersionId.ShouldBeNull("Binding must not overwrite the accepted original Post evidence.");
			var binding = await GetRequiredService<IRepository<FinanceExternalSettlementPolicy, Guid>>()
				.GetAsync(bound.SettlementPolicyBindingId.Value);
			binding.SourceLinkId.ShouldBe(link.Id);
			binding.CollectionPolicyVersionId.ShouldBe(fixture.CollectionPolicyVersionId);
		});
		await AssertNoTypedCollectionsAsync();
	}

	[Fact]
	public async Task Operator_Approval_Context_And_Command_Should_Recheck_A_Revoked_Action_Despite_An_Old_Revision()
	{
		var fixture = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Student);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.Owner.TenantId);
		using var actor = SignIn(fixture.Reviewer);
		var roleName = $"source-reviewer-{fixture.Reviewer.ActorId:N}";
		await RevokeOperatorCollectionViewAsync(fixture.Reviewer, roleName);
		(await OperatorGrantedAsync(fixture.Reviewer,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve)).ShouldBeTrue();
		var context = await WithUnitOfWorkAsync(() => OperatorService(fixture.Reviewer)
			.GetApprovalContextAsync(fixture.Post.Source));
		context.CanExecute.ShouldBeTrue();
		context.Revision.ShouldNotBeNullOrWhiteSpace();
		await WithUnitOfWorkAsync(() => GetRequiredService<IPermissionManager>().SetAsync(
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve, "R", roleName, false));
		(await OperatorGrantedAsync(fixture.Reviewer,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve)).ShouldBeFalse();

		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() =>
			OperatorService(fixture.Reviewer).GetApprovalContextAsync(fixture.Post.Source)));
		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() =>
			TypedService(fixture.Reviewer).ApproveAsync(new()
			{
				Source = context.Source,
				Revision = context.Revision,
				Reason = "An old revision cannot restore revoked approval authority.",
				IdempotencyKey = Guid.NewGuid().ToString("N")
			})));

		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<Charge, Guid>>().GetAsync(fixture.Posted.FinanceChargeId))
				.Status.ShouldBe(ChargeStatus.Pending);
			var link = (await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>()
				.GetListAsync(row => row.TenantId == fixture.Owner.TenantId && row.SourceId == fixture.Owner.AssessmentId))
				.Single();
			(await GetRequiredService<IRepository<FinanceExternalReceivableCommand, Guid>>()
				.CountAsync(row => row.TenantId == fixture.Owner.TenantId && row.SourceLinkId == link.Id && row.Operation == "Approve"))
				.ShouldBe(0);
		});
		await AssertNoTypedCollectionsAsync();
	}

	// Shared by the sibling operator options/history drafts. Explicitly pass the
	// authority built through FinanceServices so nested source checks use its real
	// persisted-role permission checker rather than the test module's default one.
	private IFinanceExternalOperatorAppService OperatorService(Fixture fixture)
	{
		var services = new FinanceServices(ServiceProvider, fixture, useStoredSource: true);
		var authority = ActivatorUtilities.CreateInstance<FinanceExternalSettlementAuthority>(services);
		var service = ActivatorUtilities.CreateInstance<FinanceExternalOperatorAppService>(services, authority);
		service.LazyServiceProvider = new AbpLazyServiceProvider(services);
		return service;
	}

	private async Task RevokeOperatorCollectionViewAsync(Fixture actor, string roleName)
	{
		await WithUnitOfWorkAsync(() => GetRequiredService<IPermissionManager>().SetAsync(
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View, "R", roleName, false));
		(await OperatorGrantedAsync(actor, ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View))
			.ShouldBeFalse();
		(await OperatorGrantedAsync(actor, ShikshaMitraSubmodulePermissions.Library.Charges.View)).ShouldBeTrue();
	}

	private Task<bool> OperatorGrantedAsync(Fixture actor, string action) => WithUnitOfWorkAsync(() =>
		new FinanceServices(ServiceProvider, actor, useStoredSource: true)
			.GetRequiredService<IPermissionChecker>().IsGrantedAsync(action));

	private static void AssertOperatorSource(FinanceExternalOperatorContextDto context, TypedSettlement fixture)
	{
		context.Source.Kind.ShouldBe(FinanceExternalSourceKind.LibraryAssessment);
		context.Source.SourceId.ShouldBe(fixture.Owner.AssessmentId);
		context.Source.SourceVersion.ShouldBe(fixture.Owner.Version);
		context.Receivable.FinanceChargeId.ShouldBe(fixture.Posted.FinanceChargeId);
		context.Receivable.AcademicYearId.ShouldBe(fixture.Owner.YearId);
		context.Receivable.Payer.Kind.ShouldBe(fixture.Owner.PayerKind);
		context.Receivable.OriginalAmount.ShouldBe(100.75m);
		context.Receivable.SettledPaymentAmount.ShouldBe(0m);
	}
}
