using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Reconciliation;
using ShikshaMitra.Finance.Accounts.Reconciliation.Dtos;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private async Task<MovedVerifiedBankReceiptFixture> SeedMovedVerifiedBankReceiptAsync()
	{
		MovedCashSource? source = null;
		var settlement = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee, prepareSource: async owner =>
		{
			source = await PrepareMovedCashLoanSourceAsync(owner);
		});
		var recordedSource = source ?? throw new InvalidOperationException("A sealed staff loan must exist before the actual Finance Post.");
		var owner = settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		await ConfigureAndApproveTypedAsync(settlement);
		var bank = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BankAccount, Guid>>().InsertAsync(new BankAccount(
			Guid.NewGuid(), owner.YearId, "School Receiving Bank", "School branch", null,
			"Configured School", BankAccountType.Current, "1234567890", "Library recovery collection", "TEST0000001", null,
			tenantId: owner.TenantId), autoSave: true));
		await WithUnitOfWorkAsync(async () =>
		{
			var reviewer = await GetRequiredService<IIdentityUserRepository>().GetAsync(settlement.Reviewer.ActorId, includeDetails: true);
			var roleIds = reviewer.Roles.Select(row => row.RoleId).ToArray();
			var roles = await GetRequiredService<IRepository<IdentityRole, Guid>>().GetListAsync(row =>
				row.TenantId == owner.TenantId && Enumerable.Contains(roleIds, row.Id));
			roles.ShouldNotBeEmpty();
			foreach (var role in roles)
				foreach (var permission in new[]
				{
					ShikshaMitraSubmodulePermissions.Finance.Accounts_Reconciliation.View,
					ShikshaMitraSubmodulePermissions.Finance.Accounts_Reconciliation.Add
				}) await GetRequiredService<IPermissionManager>().SetAsync(permission, "R", role.Name, true);
		});
		var form = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(settlement.Post.Source));
		var collection = await WithUnitOfWorkAsync(() => TypedService(owner).CollectAsync(new()
		{
			Source = settlement.Post.Source,
			Revision = form.Revision,
			IdempotencyKey = Guid.NewGuid().ToString("N"),
			SchoolReceiptDate = new(2026, 11, 17),
			Payments = [new()
			{
				PaymentMethod = PaymentMethodEnum.UPI, Amount = 40.25m, BankAccountId = bank.Id,
				Details = new() { ["upiTransactionId"] = "FINAL-UPI-84", ["upiId"] = "school@bank" }
			}]
		}));
		collection.ReceiptKind.ShouldBe(FeeReceiptKind.ProvisionalCharge);
		collection.Receivable.SettledPaymentAmount.ShouldBe(0m);
		collection.Receivable.PendingVerificationAmount.ShouldBe(40.25m);
		using (SignIn(settlement.Reviewer))
		{
			var services = new DownstreamRefundServices(new FinanceServices(ServiceProvider, settlement.Reviewer, useStoredSource: true));
			await WithUnitOfWorkAsync(async () =>
			{
				var permissions = services.GetRequiredService<IPermissionChecker>();
				(await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Accounts_Reconciliation.View)).ShouldBeTrue();
				(await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Accounts_Reconciliation.Add)).ShouldBeTrue();
				(await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Charges.View)).ShouldBeTrue();
			});
			var reconciliation = ActivatorUtilities.CreateInstance<ReconciliationAppService>(services);
			reconciliation.LazyServiceProvider = new AbpLazyServiceProvider(services);
			var verified = await WithUnitOfWorkAsync(() => reconciliation.ApproveAsync(collection.ReceiptId,
				new ApproveReconciliationInput { Remarks = "Verified actual bank evidence before changing the staff placement." }));
			verified.Success.ShouldBeTrue();
		}
		var finalReceiptId = await WithUnitOfWorkAsync(async () =>
		{
			var provisional = await GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().GetAsync(collection.ReceiptId);
			provisional.LinkedFinalReceiptId.ShouldNotBeNull();
			return provisional.LinkedFinalReceiptId!.Value;
		});
		var settled = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(settlement.Post.Source));
		settled.Receivable.SettledPaymentAmount.ShouldBe(40.25m);
		settled.Receivable.PendingVerificationAmount.ShouldBe(0m);
		settled.Receivable.OutstandingAmount.ShouldBe(60.50m);
		await WithUnitOfWorkAsync(async () =>
		{
			var assignments = GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>();
			await assignments.DeleteAsync(row => row.TenantId == owner.TenantId && row.EmployeeId == owner.PersonId, true);
			await assignments.InsertAsync(new(Guid.NewGuid(), owner.TenantId, owner.PersonId, recordedSource.Current.StageId), true);
		});
		return new(settlement, collection, finalReceiptId, recordedSource);
	}

	private Task SetFinalBankReceiptReaderScopeAsync(MovedVerifiedBankReceiptFixture fixture, bool currentPlacement) =>
		WithUnitOfWorkAsync(async () =>
		{
			var owner = fixture.Settlement.Owner;
			var placement = currentPlacement ? fixture.Source.Current : fixture.Source.Original;
			var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await assignments.DeleteAsync(row => row.TenantId == owner.TenantId && row.UserId == owner.ActorId, true);
			await assignments.InsertAsync(new(Guid.NewGuid(), owner.TenantId, userId: owner.ActorId,
				academicYearId: fixture.Source.LoanYearId, educationBoardId: placement.BoardId, educationMediumId: placement.MediumId,
				educationMediumStageId: placement.StageId, description: "Current final-receipt reader authority"), true);
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(owner.ActorId);
		});

	private sealed record MovedVerifiedBankReceiptFixture(TypedSettlement Settlement,
		FinanceExternalCollectionDto Collection, Guid FinalReceiptId, MovedCashSource Source);
}
