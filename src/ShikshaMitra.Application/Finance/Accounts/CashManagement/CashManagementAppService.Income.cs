using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Transactions;
using Volo.Abp;

namespace ShikshaMitra.Finance.Accounts.CashManagement;

public partial class CashManagementAppService
{
	private async Task ApplyDepositIncomeAsync(CashCollection cash, BankAccount bank, CashDepositRecord? deposit)
	{
		var sharePrefix = cash.IncomeSourceKeyPrefix + "bank-share:";
		var entries = cash.IncomeTransactionId.HasValue
			? await transactions.GetListAsync(x => x.Id == cash.IncomeTransactionId.Value)
			: await transactions.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.SourceId == cash.SourceId &&
				x.SourceKey != null && x.SourceKey.StartsWith(cash.IncomeSourceKeyPrefix) && !x.SourceKey.StartsWith(sharePrefix));
		if (entries.Count == 0 || entries.Any(x => x.TenantId != CurrentTenant.Id || x.AcademicYearId != cash.AcademicYearId ||
			x.TransactionType != TransactionType.Income || x.PaymentMethod != 0 || x.IsConfirmed || x.Amount <= 0 ||
			x.ExtraProperties.ContainsKey(ShikshaMitra.Finance.Accounts.Incomes.Transaction.OriginalCashGrossAmountKey) ||
			x.CashCollectionId.HasValue && x.CashCollectionId != cash.Id) || entries.Sum(x => x.Amount) != cash.Amount)
			throw new BusinessException(CashManagementErrorCodes.InvalidSource);
		var ordered = entries.OrderBy(x => x.SourceKey, StringComparer.Ordinal).ThenBy(x => x.Id).ToArray();
		var deposited = deposit?.Amount ?? 0m;
		var previouslyDeposited = cash.DepositedAmount - deposited;
		var closesSource = cash.Status == 1;
		if (closesSource && previouslyDeposited == 0m)
		{
			var amounts = CashIncomeAllocator.Allocate(ordered.Select(x => x.Amount).ToArray(), deposited);
			for (var index = 0; index < ordered.Length; index++)
				await FinalizeCashIncomeEntryAsync(ordered[index], cash, bank, amounts[index]);
			return;
		}

		// A split cash source may be deposited into several banks. Keep each confirmed
		// Income share attached to its own deposit and preserve the original gross evidence.
		var priorQuery = (await transactions.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == cash.AcademicYearId && x.CashCollectionId == cash.Id &&
			x.TransactionType == TransactionType.Income && x.IsConfirmed && x.Amount > 0);
		if (cash.IncomeTransactionId is { } manualIncomeId)
			priorQuery = priorQuery.Where(x => x.Id != manualIncomeId && x.SourceKey == null);
		else
			priorQuery = priorQuery.Where(x => x.SourceKey != null && x.SourceKey.StartsWith(sharePrefix));
		var prior = await AsyncExecuter.ToListAsync(priorQuery);
		if (prior.Sum(x => x.Amount) != previouslyDeposited)
			throw new BusinessException(CashManagementErrorCodes.InvalidSource);
		if (deposit != null)
		{
			var allocated = prior.ToLookup(x =>
			{
				if (cash.IncomeTransactionId is { } manualIncome)
					return manualIncome.ToString("N");
				var key = x.SourceKey!;
				return key[(key.LastIndexOf(':') + 1)..];
			});
			var available = ordered.Select(entry => (Entry: entry,
				Amount: entry.Amount - allocated[entry.Id.ToString("N")].Sum(posted => posted.Amount))).ToArray();
			if (available.Any(row => row.Amount < 0))
				throw new BusinessException(CashManagementErrorCodes.InvalidSource);
			var payable = available.Where(row => row.Amount > 0).ToArray();
			var amounts = CashIncomeAllocator.Allocate(payable.Select(row => row.Amount).ToArray(), deposited);
			var bankLabel = BankLabel(bank);
			for (var index = 0; index < payable.Length; index++)
			{
				if (amounts[index] == 0) continue;
				var original = payable[index].Entry;
				var share = original.CreateCashDepositShare(GuidGenerator.Create(), cash.Id, bank.Id, bankLabel,
					amounts[index], original.IsAutomatic ? $"{sharePrefix}{deposit.Id:N}:{original.Id:N}" : null);
				await transactions.InsertAsync(share);
			}
		}
		if (closesSource)
			foreach (var entry in ordered)
				await FinalizeCashIncomeEntryAsync(entry, cash, bank, 0m);
	}

	private async Task FinalizeCashIncomeEntryAsync(Transaction entry, CashCollection cash, BankAccount bank, decimal amount)
	{
		if (!entry.CashCollectionId.HasValue)
			entry.LinkCashCollection(cash.Id, studentId: entry.StudentId, financeFeeAccountId: entry.FinanceFeeAccountId,
				employeeId: entry.EmployeeId);
		entry.ApproveCashIncome(bank.Id, BankLabel(bank), amount);
		await transactions.UpdateAsync(entry);
	}
}
