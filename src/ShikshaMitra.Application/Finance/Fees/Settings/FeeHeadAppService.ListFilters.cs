using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Settings.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeHeadAppService
{
	private static readonly SettingsTableFilter<FeeHeadResponse> ListFilters = new(
		new Dictionary<string, LambdaExpression>
		{
			["name"] = (Expression<Func<FeeHeadResponse, string>>)(row => row.Name),
			["feeCategoryName"] = (Expression<Func<FeeHeadResponse, string>>)(row => row.FeeCategoryName),
			["incomeCategoryName"] = (Expression<Func<FeeHeadResponse, string>>)(row =>
				string.IsNullOrEmpty(row.IncomeCategoryName) ? "-" : row.IncomeCategoryName),
			["description"] = (Expression<Func<FeeHeadResponse, string>>)(row =>
				string.IsNullOrEmpty(row.Description) ? "-" : row.Description),
			["isActive"] = (Expression<Func<FeeHeadResponse, int>>)(row => row.IsActive ? 1 : 0)
		});
}
