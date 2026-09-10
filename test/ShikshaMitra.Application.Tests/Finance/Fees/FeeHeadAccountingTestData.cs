using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees;

internal static class FeeHeadAccountingTestData
{
	public static async Task<Guid> CreateIncomeCategoryAsync(
		IRepository<AccountHead, Guid> accountHeadRepository,
		IRepository<TransactionCategory, Guid> incomeCategoryRepository,
		Guid academicYearId,
		Guid? tenantId,
		string label)
	{
		var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
		var accountHead = new AccountHead(
			Guid.NewGuid(), tenantId, academicYearId, $"{label} Revenue",
			AccountHeadType.Income, null, null);
		await accountHeadRepository.InsertAsync(accountHead, autoSave: true);
		var category = new TransactionCategory(
			Guid.NewGuid(), tenantId, academicYearId, TransactionType.Income, $"{label} Income {suffix}",
			null, [], true, $"TEST-INC-{suffix}", accountHead.Id,
			DateTimeOffset.UtcNow.AddDays(-1), null);
		await incomeCategoryRepository.InsertAsync(category, autoSave: true);
		return category.Id;
	}
}
