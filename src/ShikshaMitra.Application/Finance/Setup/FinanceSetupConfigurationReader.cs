using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.AcademicSetups;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Setup;

[RemoteService(false)]
public class FinanceSetupConfigurationReader(
	IRepository<TransactionCategory, Guid> categories, IRepository<BankAccount, Guid> banks,
	IRepository<FeeCategory, Guid> fees, IRepository<FeeHead, Guid> heads, IRepository<PaymentTerm, Guid> terms,
	IRepository<ChargeCategory, Guid> charges, IRepository<ConcessionType, Guid> concessions,
	IRepository<CollectionPolicy, Guid> collectionPolicies, IRepository<RefundPolicy, Guid> refundPolicies,
	IRepository<ConcessionPolicy, Guid> concessionPolicies, IRepository<ProrationPolicy, Guid> prorationPolicies,
	IRepository<FeeStructure, Guid> structures, IRepository<FeeStructureMetadata, Guid> metadata,
	IRepository<Standard, Guid> standards, IUserScopeAccessService scopes, AcademicHierarchyQueryScope hierarchyScopes) : ApplicationService
{
	public async Task ReadAsync(List<FinanceSetupSettingStateDto> settings, Guid yearId)
	{
		foreach (var setting in settings)
		{
			// Never read configuration belonging to a setting whose exact View is absent.
			if (!setting.CanView) continue;
			var tenantId = CurrentTenant.Id;
			var configured = setting.Key switch {
				"income-categories" => await categories.AnyAsync(x => x.TenantId == tenantId && x.AcademicYearId == yearId && x.CategoryType == TransactionType.Income && x.IsActive),
				"expense-categories" => await categories.AnyAsync(x => x.TenantId == tenantId && x.AcademicYearId == yearId && x.CategoryType == TransactionType.Expense && x.IsActive),
				"bank-accounts" => await banks.AnyAsync(x => x.TenantId == tenantId && x.AcademicYearId == yearId && x.IsActive),
				"fee-categories" => await HasFeeCategoriesAsync(yearId),
				"payment-terms" => await HasPaymentTermsAsync(yearId),
				"charge-categories" => await charges.AnyAsync(x => x.TenantId == tenantId && x.AcademicYearId == yearId && x.IsActive),
				"concession-types" => await concessions.AnyAsync(x => x.TenantId == tenantId && x.AcademicYearId == yearId && x.IsActive),
				"collection-policy" => await collectionPolicies.AnyAsync(x => x.TenantId == tenantId && x.AcademicYearId == yearId && x.Status == CollectionPolicyStatus.Active),
				"refund-policy" => await refundPolicies.AnyAsync(x => x.TenantId == tenantId && x.AcademicYearId == yearId && x.Status == RefundPolicyStatus.Active),
				"concession-policy" => await concessionPolicies.AnyAsync(x => x.TenantId == tenantId && x.AcademicYearId == yearId && x.Status == ConcessionPolicyStatus.Active),
				"proration-policy" => await prorationPolicies.AnyAsync(x => x.TenantId == tenantId && x.AcademicYearId == yearId && x.Status == ProrationPolicyStatus.Active),
				"fee-structures" => await HasStructuresAsync(yearId),
				_ => throw new BusinessException(FinanceSetupKeys.Invalid)
			};
			setting.ConfigurationState = configured ? "configured" : "empty";
		}
	}

	private async Task<bool> HasPaymentTermsAsync(Guid yearId)
	{
		var scope = await scopes.GetCurrentUserScopeAsync();
		var boardIds = scope.BoardIds.ToArray();
		var yearWide = scope.HasAcademicYearWideScope(yearId);
		return await terms.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId &&
			x.Status == PaymentTermStatus.Active && x.Versions.Any(v => v.Status == PaymentTermVersionStatus.Published) &&
			(yearWide || Enumerable.Contains(boardIds, x.BoardId)));
	}

	private async Task<bool> HasFeeCategoriesAsync(Guid yearId)
	{
		var activeCategories = (await fees.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && x.IsActive);
		var activeHeads = (await heads.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && x.IsActive);
		return await AsyncExecuter.AnyAsync(from category in activeCategories
			join head in activeHeads on category.Id equals head.FeeCategoryId
			select category.Id);
	}

	private async Task<bool> HasStructuresAsync(Guid yearId)
	{
		// Ancestor IDs describe a class assignment's placement, not access to sibling classes.
		// Reuse the academic hierarchy's exact wildcard-boundary-aware class projection.
		var classes = (await hierarchyScopes.StandardsAsync(await standards.GetQueryableAsync()))
			.Where(x => x.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId == yearId)
			.Select(x => new {
				StandardId = x.Id, EducationMediumId = x.EducationMediumStage.EducationMediumId,
				EducationBoardId = x.EducationMediumStage.EducationMedium.EducationBoardId
			});
		var containers = (await metadata.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId);
		var published = (await structures.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.IsLocked && x.FeeStructureDetails.Any());
		return await AsyncExecuter.AnyAsync(from structure in published
			join container in containers on structure.FeeStructureMetadataId equals container.Id
			join standard in classes on structure.StandardId equals standard.StandardId
			where container.AcademicBoardId == standard.EducationBoardId && container.AcademicMediumId == standard.EducationMediumId
			select structure.Id);
	}
}
