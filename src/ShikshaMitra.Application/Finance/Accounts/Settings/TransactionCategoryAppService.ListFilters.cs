using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Domain.Finance.Accounts.Settings;

namespace ShikshaMitra.Finance.Accounts.Settings;

public partial class TransactionCategoryAppService
{
	private static readonly SettingsTableFilter<TransactionCategory> ListFilter = new(
		new Dictionary<string, LambdaExpression>
		{
			["name"] = (Expression<Func<TransactionCategory, string>>)(x => x.Name),
			["description"] = (Expression<Func<TransactionCategory, string?>>)(x => x.Description == null || x.Description == "" ? "-" : x.Description),
			["isActive"] = (Expression<Func<TransactionCategory, int>>)(x => x.IsActive ? 1 : 0)
		});
}
