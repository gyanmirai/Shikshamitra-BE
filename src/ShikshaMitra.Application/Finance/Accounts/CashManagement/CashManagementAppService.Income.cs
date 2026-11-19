using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using Volo.Abp;

namespace ShikshaMitra.Finance.Accounts.CashManagement;

public partial class CashManagementAppService
{
	private async Task FinalizeIncomeAsync(CashCollection cash, BankAccount bank)
	{
		var entries = cash.IncomeTransactionId.HasValue
			? await transactions.GetListAsync(x => x.Id == cash.IncomeTransactionId.Value)
			: await transactions.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.SourceId == cash.SourceId &&
				x.SourceKey != null && x.SourceKey.StartsWith(cash.IncomeSourceKeyPrefix));
		if (entries.Count == 0 || entries.Any(x => x.TenantId != CurrentTenant.Id || x.AcademicYearId != cash.AcademicYearId ||
			x.TransactionType != TransactionType.Income || x.PaymentMethod != 0 || x.IsConfirmed || x.Amount <= 0 ||
			x.ExtraProperties.ContainsKey(ShikshaMitra.Finance.Accounts.Incomes.Transaction.OriginalCashGrossAmountKey) ||
			x.CashCollectionId.HasValue && x.CashCollectionId != cash.Id) || entries.Sum(x => x.Amount) != cash.Amount)
			throw new BusinessException(CashManagementErrorCodes.InvalidSource);
		var ordered = entries.OrderBy(x => x.SourceKey, StringComparer.Ordinal).ThenBy(x => x.Id).ToArray();
		var amounts = CashIncomeAllocator.Allocate(ordered.Select(x => x.Amount).ToArray(), cash.DepositedAmount);
		for (var index = 0; index < ordered.Length; index++)
		{
			var entry = ordered[index];
			if (!entry.CashCollectionId.HasValue)
				entry.LinkCashCollection(cash.Id, studentId: entry.StudentId, financeFeeAccountId: entry.FinanceFeeAccountId);
			entry.ApproveCashIncome(bank.Id, BankLabel(bank), amounts[index]);
			await transactions.UpdateAsync(entry);
		}
	}
}
