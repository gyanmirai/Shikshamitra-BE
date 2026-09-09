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
	IRepository<AccountHead, Guid> accountHeadRepository) : DomainService
{
	public async Task<FeeHeadAccountingMapping> GetValidMappingAsync(
		Guid academicYearId, Guid incomeCategoryId)
	{
		var category = await incomeCategoryRepository.FindAsync(incomeCategoryId);
		if (category is null || category.AcademicYearId != academicYearId ||
			category.CategoryType != TransactionType.Income || !category.IsActive ||
			!category.DefaultAccountHeadId.HasValue)
		{
			throw InvalidMapping("Select an active Income Category from the same academic year.");
		}

		var accountHead = await accountHeadRepository.FindAsync(category.DefaultAccountHeadId.Value);
		if (accountHead is null || accountHead.AcademicYearId != academicYearId ||
			accountHead.HeadType != AccountHeadType.Income || !accountHead.IsActive)
		{
			throw InvalidMapping("The Income Category must use an active Income Account Head.");
		}

		return new FeeHeadAccountingMapping(category, accountHead);
	}

	private static BusinessException InvalidMapping(string reason) =>
		new BusinessException(FinanceFeeHeadErrorCodes.InvalidIncomeCategory).WithData("Reason", reason);
}

public sealed record FeeHeadAccountingMapping(TransactionCategory IncomeCategory, AccountHead AccountHead);
