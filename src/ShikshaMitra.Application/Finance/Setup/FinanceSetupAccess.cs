using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FinancePermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Finance.Setup;

[RemoteService(false)]
public class FinanceSetupAccess(
	IIdentityUserRepository users, IRepository<AcademicYear, Guid> years,
	FinanceScopeAccessService scopeAccess, IPermissionChecker permissions) : ApplicationService
{
	private static readonly IReadOnlyDictionary<string, (string View, string Add, string? Edit)> Policies = new Dictionary<string, (string, string, string?)> {
		["income-categories"] = (FinancePermissions.Accounts_Settings_IncomeCategory.View, FinancePermissions.Accounts_Settings_IncomeCategory.Add, FinancePermissions.Accounts_Settings_IncomeCategory.Edit),
		["expense-categories"] = (FinancePermissions.Accounts_Settings_ExpenseCategory.View, FinancePermissions.Accounts_Settings_ExpenseCategory.Add, FinancePermissions.Accounts_Settings_ExpenseCategory.Edit),
		["bank-accounts"] = (FinancePermissions.Accounts_Settings_BankAccount.View, FinancePermissions.Accounts_Settings_BankAccount.Add, FinancePermissions.Accounts_Settings_BankAccount.Edit),
		["fee-categories"] = (FinancePermissions.Fees_Settings_FeeCategory.View, FinancePermissions.Fees_Settings_FeeCategory.Add, null),
		["payment-terms"] = (FinancePermissions.Fees_Settings_PaymentTerms.View, FinancePermissions.Fees_Settings_PaymentTerms.Add, null),
		["charge-categories"] = (FinancePermissions.Fees_Settings_ChargeCategory.View, FinancePermissions.Fees_Settings_ChargeCategory.Add, FinancePermissions.Fees_Settings_ChargeCategory.Edit),
		["concession-types"] = (FinancePermissions.Fees_Settings_ConcessionType.View, FinancePermissions.Fees_Settings_ConcessionType.Add, FinancePermissions.Fees_Settings_ConcessionType.Edit),
		["collection-policy"] = (FinancePermissions.Policies_CollectionPolicy.View, FinancePermissions.Policies_CollectionPolicy.Add, null),
		["refund-policy"] = (FinancePermissions.Policies_RefundPolicy.View, FinancePermissions.Policies_RefundPolicy.Add, null),
		["concession-policy"] = (FinancePermissions.Policies_ConcessionPolicy.View, FinancePermissions.Policies_ConcessionPolicy.Add, null),
		["proration-policy"] = (FinancePermissions.Policies_ProrationPolicy.View, FinancePermissions.Policies_ProrationPolicy.Add, null),
		["fee-structures"] = (FinancePermissions.Fees_Settings_FeeStructure.View, FinancePermissions.Fees_Settings_FeeStructure.Add, null),
	};
	private static readonly string[] OperationalViews = [
		FinancePermissions.Fees_StructureAssign.View,
		FinancePermissions.Fees_FeeCollections.View,
		FinancePermissions.Fees_Charges.View,
		FinancePermissions.Fees_Refund.View,
		FinancePermissions.Fees_Concession.View,
		FinancePermissions.Fees_InvoiceReceipt.View,
		FinancePermissions.Accounts_IncomeTracking.View,
		FinancePermissions.Accounts_ExpenseTracking.View,
		FinancePermissions.Accounts_CashManagement.View,
		FinancePermissions.Accounts_Reconciliation.View,
		FinancePermissions.Auditing.View
	];

	public async Task<Guid> RequireIdentityAsync()
	{
		if (!CurrentUser.IsAuthenticated || CurrentTenant.Id is not Guid tenantId || CurrentUser.TenantId != tenantId || CurrentUser.Id is not Guid actorId)
			throw new AbpAuthorizationException();
		var actor = await users.FindAsync(actorId);
		if (actor?.TenantId != tenantId || !actor.IsActive) throw new AbpAuthorizationException();
		return tenantId;
	}

	public async Task<List<FinanceSetupSettingStateDto>> ReadPermissionsAsync()
	{
		var result = new List<FinanceSetupSettingStateDto>();
		foreach (var (key, policy) in Policies)
		{
			var view = await permissions.IsGrantedAsync(policy.View);
			result.Add(new() { Key = key, CanView = view,
				CanManage = view && (await permissions.IsGrantedAsync(policy.Add) || policy.Edit != null && await permissions.IsGrantedAsync(policy.Edit)) });
		}
		return result;
	}

	public async Task RequireGuidePermissionAsync()
	{
		if ((await ReadPermissionsAsync()).Any(x => x.CanView)) return;
		foreach (var permission in OperationalViews)
			if (await permissions.IsGrantedAsync(permission)) return;
		throw new AbpAuthorizationException();
	}

	public async Task<AcademicYear> LoadContextAsync(Guid yearId)
	{
		var tenantId = await RequireIdentityAsync();
		var year = await years.FindAsync(x => x.Id == yearId && x.TenantId == tenantId) ?? throw new EntityNotFoundException(typeof(AcademicYear), yearId);
		await scopeAccess.EnsureCollectionYearAccessibleAsync(yearId);
		return year;
	}
}
