using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Domain.Finance.Accounts.Settings;

namespace ShikshaMitra.Finance.Accounts.Settings;

public partial class BankAccountAppService
{
	private static readonly SettingsTableFilter<BankAccount> ListFilter = new(
		new Dictionary<string, LambdaExpression>
		{
			["bankName"] = (Expression<Func<BankAccount, string>>)(x => x.BankName),
			["branchName"] = (Expression<Func<BankAccount, string>>)(x => x.BranchName),
			["branchAddress"] = (Expression<Func<BankAccount, string?>>)(x => x.BranchAddress == null || x.BranchAddress == "" ? "-" : x.BranchAddress),
			["accountHolderName"] = (Expression<Func<BankAccount, string>>)(x => x.AccountHolderName),
			["accountType"] = (Expression<Func<BankAccount, int>>)(x => (int)x.AccountType),
			["maskedAccountNumber"] = (Expression<Func<BankAccount, string>>)(x => Mask(x.AccountNumber)),
			["purpose"] = (Expression<Func<BankAccount, string?>>)(x => x.Purpose),
			["ifscCode"] = (Expression<Func<BankAccount, string>>)(x => MaskIfsc(x.IfscCode)),
			["maskedUpiId"] = (Expression<Func<BankAccount, string>>)(x => MaskUpi(x.UpiId) ?? "-"),
			["isActive"] = (Expression<Func<BankAccount, int>>)(x => x.IsActive ? 1 : 0)
		});

	private async Task<IQueryable<BankAccount>> ApplyListFiltersAsync(IQueryable<BankAccount> query, FinanceTableQuery filters)
	{
		if (filters.Filters.Count == 0) return query;
		// Evaluate display-only fields across the complete authorized candidate set before paging.
		var candidates = await AsyncExecuter.ToListAsync(query);
		var ids = ListFilter.Apply(candidates.AsQueryable(), filters).Select(x => x.Id).ToArray();
		return query.Where(x => Enumerable.Contains(ids, x.Id));
	}
}
