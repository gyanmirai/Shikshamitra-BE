using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private async Task<DownstreamRefundFixture> SeedSubmittedTypedRefundAsync(FinanceExternalPayerKind kind,
		Func<Fixture, Task>? prepareSource = null)
	{
		var source = await SeedTypedRefundAsync(kind, prepareSource: prepareSource);
		var owner = source.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var form = await WithUnitOfWorkAsync(() => TypedRefundService(owner).GetFormAsync(source.Settlement.Post.Source));
		var submitted = await WithUnitOfWorkAsync(() => TypedRefundService(owner).CreateAsync(RefundRequest(source, form.Revision, 15.25m)));
		submitted.Status.ShouldBe(FeeRefundStatus.Submitted);
		submitted.TotalAmount.ShouldBe(15.25m);
		submitted.CalculatedAmount.ShouldBe(15m);
		var authorization = new DownstreamRefundServices(new FinanceServices(ServiceProvider, owner, useStoredSource: true))
			.GetRequiredService<IAuthorizationService>();
		(await authorization.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)).ShouldBeTrue();
		(await authorization.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve)).ShouldBeFalse();
		return await WithUnitOfWorkAsync(async () =>
		{
			var actorId = Guid.NewGuid();
			var role = new IdentityRole(Guid.NewGuid(), "admin", owner.TenantId);
			await GetRequiredService<IIdentityRoleRepository>().InsertAsync(role, true);
			var admin = new IdentityUser(actorId, $"refund-admin-{actorId:N}", $"{actorId:N}@example.test", owner.TenantId);
			admin.AddRole(role.Id);
			await GetRequiredService<IIdentityUserRepository>().InsertAsync(admin, true);
			await GrantTypedActionsAsync(role.Name);
			foreach (var permission in new[]
			{
				ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View,
				ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve,
				ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Process,
				ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Reverse
			}) await GetRequiredService<IPermissionManager>().SetAsync(permission, "R", role.Name, true);
			await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), owner.TenantId, userId: actorId, academicYearId: owner.YearId,
				description: "Independent actual school administrator for the source refund"), true);
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actorId);
			(await GetRequiredService<IUserScopeService>().IsAdminUserAsync(actorId)).ShouldBeTrue();
			(await GetRequiredService<IUserScopeService>().IsAdminUserAsync(owner.ActorId)).ShouldBeFalse();
			var category = new TransactionCategory(Guid.NewGuid(), owner.TenantId, owner.YearId,
				TransactionType.Expense, "Recorded Library refund", null);
			await GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(category, true);
			var bank = new BankAccount(Guid.NewGuid(), owner.YearId, "School Refund Bank", "School branch", null,
				"Configured School", BankAccountType.Current, "1234567890", "Recorded refund payout", "TEST0000001", null,
				tenantId: owner.TenantId);
			await GetRequiredService<IRepository<BankAccount, Guid>>().InsertAsync(bank, true);
			var authority = owner with { ActorId = actorId };
			using (SignIn(authority))
			{
				var permissions = new FinanceServices(ServiceProvider, authority, useStoredSource: true).GetRequiredService<IPermissionChecker>();
				(await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View)).ShouldBeTrue();
				(await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve)).ShouldBeTrue();
				(await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Process)).ShouldBeTrue();
			}
			return new DownstreamRefundFixture(source, submitted, authority, category.Id, bank.Id);
		});
	}

	private FeeRefundAppService DownstreamRefundService(Fixture actor)
	{
		var services = new DownstreamRefundServices(new FinanceServices(ServiceProvider, actor, useStoredSource: true));
		var service = ActivatorUtilities.CreateInstance<FeeRefundAppService>(services);
		service.LazyServiceProvider = new AbpLazyServiceProvider(services);
		return service;
	}

	// A named aggregate transition provides approved input for payout-only tests.
	// The separate approval test exercises the actual application review boundary.
	private Task SeedApprovedRefundStateAsync(DownstreamRefundFixture fixture) => WithUnitOfWorkAsync(async () =>
	{
		var repository = GetRequiredService<IFeeRefundRepository>();
		var request = await repository.FindWithLinesAsync(fixture.Submitted.Id)
			?? throw new InvalidOperationException("The typed submitted request is missing.");
		var line = request.Lines.Single();
		var reservation = new FeeRefundReservation(Guid.NewGuid(), request.Id, line.Id, null,
			fixture.Source.AllocationId, 15m, fixture.Source.Settlement.Posted.FinanceChargeId);
		request.Approve(fixture.Administrator.ActorId, [reservation], "Isolated historical approved input for payout verification.");
		await repository.StageNewReservationsAsync([reservation]);
	});

	private static RecordRefundPayoutDto RecordedTypedPayout(DownstreamRefundFixture fixture) => new()
	{
		IdempotencyKey = Guid.NewGuid().ToString("N"), Method = "UPI", Amount = 15m,
		PaymentDate = new DateTimeOffset(2026, 11, 18, 8, 30, 0, TimeSpan.Zero),
		ExpenseCategoryId = fixture.ExpenseCategoryId, BankAccountId = fixture.BankAccountId,
		ExternalReference = "UPI-" + Guid.NewGuid().ToString("N"),
		EvidenceReference = "School bank statement confirms this actual completed payout."
	};

	private sealed record DownstreamRefundFixture(TypedRefundFixture Source, FeeRefundRequestDto Submitted,
		Fixture Administrator, Guid ExpenseCategoryId, Guid BankAccountId);

	// Explicitly reconstruct nested services with the real DB permission checker.
	// The shared test provider otherwise installs AlwaysAllow authorization.
	private sealed class DownstreamRefundServices(IServiceProvider inner) : IServiceProvider
	{
		public object? GetService(Type type)
		{
			if (type == typeof(IServiceProvider)) return this;
			if (type == typeof(IAuthorizationService) || type == typeof(IAbpAuthorizationService))
				return ActivatorUtilities.CreateInstance<AbpAuthorizationService>(this, this);
			if (type == typeof(IAuthorizationPolicyProvider))
				return ActivatorUtilities.CreateInstance<AbpAuthorizationPolicyProvider>(this);
			if (type == typeof(IAuthorizationHandlerProvider))
				return new DefaultAuthorizationHandlerProvider([
					ActivatorUtilities.CreateInstance<PermissionRequirementHandler>(this),
					ActivatorUtilities.CreateInstance<PermissionsRequirementHandler>(this),
					new PassThroughAuthorizationHandler()]);
			if (type != typeof(FinanceScopeAccessService) && type != typeof(FinanceReviewerAccessService) &&
				type != typeof(FinanceExternalSettlementAuthority) && type != typeof(FinanceExternalRefundRequestAccess) &&
				type != typeof(FinanceExternalCashSourceScope)) return inner.GetService(type);
			var service = ActivatorUtilities.CreateInstance(this, type);
			if (service is ApplicationService application) application.LazyServiceProvider = new AbpLazyServiceProvider(this);
			return service;
		}
	}
}
