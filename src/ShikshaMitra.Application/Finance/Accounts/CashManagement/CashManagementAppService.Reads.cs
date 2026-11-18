using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Accounts.CashManagement;

public partial class CashManagementAppService
{
	private async Task<List<CashRow>> ReadRowsAsync(IQueryable<CashCollection> query,
		(bool Approve, bool Deposit, bool Credit, bool SelfApproval) flags)
	{
		// Project after paging: never hydrate expense, credit or replay graphs for register rows.
		// Only deposit identity/date metadata is needed to resolve the historical bank snapshot.
		var page = await AsyncExecuter.ToListAsync(CashManagementReadProjection.Rows(query));
		foreach (var item in page)
		{
			var bank = item.Deposits.OrderByDescending(x => x.DepositDate).ThenByDescending(x => x.Id)
				.Select(deposit => item.ExtraProperties.TryGetValue("DepositBank:" + deposit.Id.ToString("N"), out var value) && value != null
					? JsonSerializer.Deserialize<CashBankOption>(value.ToString()!) : null).FirstOrDefault(x => x != null);
			if (bank != null) { item.Row.BankAccountId = bank.Id; item.Row.BankLabel = bank.Label; }
			item.Row.CanApprove = flags.Approve && item.Row.Status == 0 && (item.Row.CreatorId != Actor || flags.SelfApproval);
			item.Row.CanDeposit = flags.Deposit && item.Row.Status == 1 && item.Row.RemainingAmount > 0;
		}
		return page.Select(x => x.Row).ToList();
	}
}
