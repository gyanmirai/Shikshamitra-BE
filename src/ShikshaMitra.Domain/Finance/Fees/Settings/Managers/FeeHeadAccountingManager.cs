using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.Settings;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Domain.Finance.Fees.Settings.Managers;

public class FeeHeadAccountingManager(
	IRepository<TransactionCategory, Guid> incomeCategoryRepository,
	ITransactionCategoryReferenceLock referenceLock) : DomainService
{
	public async Task<FeeHeadAccountingMapping> GetValidMappingAsync(
		Guid academicYearId, Guid incomeCategoryId, bool creatingReference = false)
	{
		if (creatingReference) await referenceLock.AcquireAsync();
		var category = await incomeCategoryRepository.FindAsync(incomeCategoryId);
		if (category is null || category.AcademicYearId != academicYearId ||
			category.CategoryType != TransactionType.Income || !category.IsActive)
		{
			throw InvalidMapping("Select an active Income Category from the same academic year.");
		}

		return new FeeHeadAccountingMapping(category);
	}

	private static BusinessException InvalidMapping(string reason) =>
		new BusinessException(FinanceFeeHeadErrorCodes.InvalidIncomeCategory).WithData("Reason", reason);
}

public sealed record FeeHeadAccountingMapping(TransactionCategory IncomeCategory);
