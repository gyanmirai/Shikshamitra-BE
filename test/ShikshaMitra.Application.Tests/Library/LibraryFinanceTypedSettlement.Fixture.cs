using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private async Task<TypedSettlement> SeedTypedSettlementAsync(FinanceExternalPayerKind kind,
		bool requiresWaiverApproval = false, Func<Fixture, Task>? prepareSource = null, bool departed = true)
	{
		var fixture = await SeedAsync(kind, departed: departed);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var identity = SignIn(fixture);
		if (prepareSource != null) await prepareSource(fixture);
		await WithUnitOfWorkAsync(async () =>
		{
			var charges = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var source = await charges.GetAsync(fixture.AssessmentId);
			source.ApplyUpdate(100.75m, null, null, null, null, DateTime.UtcNow);
			await charges.UpdateAsync(source, true);
		});
		fixture = await ConfirmStoredAssessmentAsync(fixture);
		var post = Post(fixture);
		var posted = await WithUnitOfWorkAsync(() => Service(fixture, useStoredSource: true).PostAsync(post));
		return await WithUnitOfWorkAsync(async () =>
		{
			await GrantTypedActionsAsync($"external-finance-{fixture.ActorId:N}");
			var reviewerId = Guid.NewGuid();
			var role = new IdentityRole(Guid.NewGuid(), $"source-reviewer-{reviewerId:N}", fixture.TenantId);
			await GetRequiredService<IIdentityRoleRepository>().InsertAsync(role, true);
			var reviewer = new IdentityUser(reviewerId, $"reviewer-{reviewerId:N}", $"{reviewerId:N}@example.test", fixture.TenantId);
			reviewer.AddRole(role.Id);
			await GetRequiredService<IIdentityUserRepository>().InsertAsync(reviewer, true);
			await GrantTypedActionsAsync(role.Name);
			await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), fixture.TenantId, userId: reviewerId, description: "Scoped source reviewer test authority"), true);
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(reviewerId);
			var makerRole = await GetRequiredService<IRepository<IdentityRole, Guid>>().FindAsync(row =>
				row.TenantId == fixture.TenantId && row.Name == $"external-finance-{fixture.ActorId:N}")
				?? throw new InvalidOperationException("The assessment maker requires its actual test role.");
			var policy = new CollectionPolicy(Guid.NewGuid(), fixture.TenantId, fixture.YearId, new CollectionPolicyUpdateModel(
				PolicyName: "Explicit Library recovery policy", PolicyCode: "LIB-RECOVERY", Description: "Reviewed recovery rules",
				Status: CollectionPolicyStatus.Active, IsDefault: false, EffectiveFrom: null, EffectiveTo: null,
				EnableLateFee: false, GracePeriodDays: 0, LateFeeApplyOnFeeHeads: [], LateFeeType: LateFeeCalculationType.Flat,
				FlatAmount: null, Percentage: null, PercentageBase: PercentageBaseType.OutstandingAmount,
				AmountPerDay: null, AmountPerWeek: null, AmountPerMonth: null, CompoundFrequency: CompoundFrequencyType.Daily,
				EscalationRules: [], FeeHeadOverrides: [], RoundingMethod: RoundingMethodType.None,
				ConsiderAsIncome: false, LateFeeIncomeHeadId: null, MaximumLateFeeAmount: null, MaximumChargingDays: null,
				EnableWaiver: false, EnableChargeWaiver: true, RequiresApproval: requiresWaiverApproval,
				ApprovalRoleIds: requiresWaiverApproval ? [role.Id, makerRole.Id] : [], AllowOnlineCollection: true));
			await GetRequiredService<IRepository<CollectionPolicy, Guid>>().InsertAsync(policy, true);
			var version = new FinancePolicyVersion(Guid.NewGuid(), fixture.TenantId, "collection", policy.Id,
				fixture.YearId, policy.Version, policy.PolicyName, policy.Status.ToString(), policy.ToSnapshot().Serialize(),
				"created", "Explicit source settlement policy");
			await GetRequiredService<IRepository<FinancePolicyVersion, Guid>>().InsertAsync(version, true);
			return new TypedSettlement(fixture, fixture with { ActorId = reviewerId }, post, posted, version.Id);
		});
	}

	private IFinanceExternalSettlementAppService TypedService(Fixture fixture)
	{
		var services = new FinanceServices(ServiceProvider, fixture, useStoredSource: true);
		var authority = ActivatorUtilities.CreateInstance<FinanceExternalSettlementAuthority>(services);
		var service = ActivatorUtilities.CreateInstance<FinanceExternalSettlementAppService>(services, authority);
		service.LazyServiceProvider = new AbpLazyServiceProvider(services);
		return service;
	}

	private async Task GrantTypedActionsAsync(string roleName)
	{
		var permissions = GetRequiredService<IPermissionManager>();
		foreach (var action in new[]
		{
			ShikshaMitraSubmodulePermissions.Library.Charges.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApplyWaiver,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApproveWaiver,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.RejectWaiver,
			ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ReverseWaiver,
			ShikshaMitraSubmodulePermissions.Finance.Fees_InvoiceReceipt.View
		}) await permissions.SetAsync(action, "R", roleName, true);
	}

	private async Task ConfigureAndApproveTypedAsync(TypedSettlement context)
	{
		using (SignIn(context.Owner))
		{
			var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
			await WithUnitOfWorkAsync(() => TypedService(context.Owner).BindPolicyAsync(new()
			{
				Source = context.Post.Source, Revision = form.Revision, CollectionPolicyVersionId = context.CollectionPolicyVersionId,
				Reason = "Confirmed the school's recovery rules.", IdempotencyKey = Guid.NewGuid().ToString("N")
			}));
		}
		using (SignIn(context.Reviewer))
		{
			var form = await WithUnitOfWorkAsync(() => TypedService(context.Reviewer).GetCollectFormAsync(context.Post.Source));
			await WithUnitOfWorkAsync(() => TypedService(context.Reviewer).ApproveAsync(new()
			{
				Source = context.Post.Source, Revision = form.Revision,
				Reason = "Reviewed the original assessment evidence.", IdempotencyKey = Guid.NewGuid().ToString("N")
			}));
		}
	}

	private sealed record TypedSettlement(Fixture Owner, Fixture Reviewer, PostFinanceExternalReceivableInput Post,
		FinanceExternalReceivableDto Posted, Guid CollectionPolicyVersionId);
}
