using System;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace ShikshaMitra.Domain.Finance.Accounts.Settings;

/// <summary>Validates business categories and school accounts without requiring account heads.</summary>
public class FinanceRoutingManager(
	IRepository<TransactionCategory, Guid> categories,
	IRepository<BankAccount, Guid> banks,
	ICurrentTenant currentTenant,
	IClock clock) : DomainService
{
	public async Task<TransactionCategory> RequireCategoryAsync(Guid academicYearId, Guid? id, TransactionType type)
	{
		if (!id.HasValue || id == Guid.Empty)
			throw new BusinessException(FinanceRoutingErrorCodes.CategoryRequired);
		var category = await categories.FindAsync(id.Value);
		if (category == null || category.TenantId != currentTenant.Id ||
			category.AcademicYearId != academicYearId || category.CategoryType != type || !category.IsActive ||
            category.EffectiveFrom > clock.Now || category.EffectiveTo < clock.Now)
			throw new BusinessException(FinanceRoutingErrorCodes.InvalidCategory);
		return category;
	}

	public async Task<BankAccount> RequireBankAsync(Guid academicYearId, Guid? id, DateTimeOffset? at = null)
	{
		if (!id.HasValue || id == Guid.Empty)
			throw new BusinessException(FinanceRoutingErrorCodes.BankRequired);
		var bank = await banks.FindAsync(id.Value);
		var instant = at ?? new DateTimeOffset(DateTime.SpecifyKind(clock.Now, DateTimeKind.Utc));
		if (bank == null || bank.TenantId != currentTenant.Id || bank.AcademicYearId != academicYearId ||
			!bank.IsActive || bank.EffectiveFrom > instant || bank.EffectiveTo < instant)
			throw new BusinessException(FinanceRoutingErrorCodes.InvalidBank);
		return bank;
	}
}
