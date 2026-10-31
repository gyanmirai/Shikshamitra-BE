using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using Volo.Abp.Validation;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class ReceiptBookAppService
{
	private static readonly IReadOnlyDictionary<string, FinanceTableColumn> ListFilterColumns =
		new Dictionary<string, FinanceTableColumn>
		{
			["name"] = new(FinanceTableValueType.Text), ["prefix"] = new(FinanceTableValueType.Text),
			["suffix"] = new(FinanceTableValueType.Text), ["board"] = new(FinanceTableValueType.Text),
			["mediumStageName"] = new(FinanceTableValueType.Text), ["description"] = new(FinanceTableValueType.Text),
			["startingNumber"] = new(FinanceTableValueType.Number), ["endingNumber"] = new(FinanceTableValueType.Number),
			["isActive"] = new(FinanceTableValueType.Enum, Values: [0, 1])
		};
	private static readonly IReadOnlyDictionary<string, LambdaExpression> ListFilterFields =
		new Dictionary<string, LambdaExpression>
		{
			["name"] = (Expression<Func<ReceiptBookResponse, string>>)(row => row.Name),
			["prefix"] = (Expression<Func<ReceiptBookResponse, string>>)(row => row.Prefix),
			["suffix"] = (Expression<Func<ReceiptBookResponse, string>>)(row => row.Suffix),
			["board"] = (Expression<Func<ReceiptBookResponse, string>>)(row => row.BoardName + "/" + row.MediumName),
			["mediumStageName"] = (Expression<Func<ReceiptBookResponse, string>>)(row => row.MediumStageName),
			["description"] = (Expression<Func<ReceiptBookResponse, string>>)(row => row.Description),
			["startingNumber"] = (Expression<Func<ReceiptBookResponse, long>>)(row => row.StartingNumber),
			["endingNumber"] = (Expression<Func<ReceiptBookResponse, long>>)(row => row.EndingNumber),
			["isActive"] = (Expression<Func<ReceiptBookResponse, int>>)(row => row.IsActive ? 1 : 0)
		};

	private static FinanceTableQuery ParseListFilters(string? filters)
	{
		try { return FinanceTableQuery.Parse(filters, null, null, null, ListFilterColumns); }
		catch (ArgumentNullException ex) when (ex.ParamName == "key")
		{
			throw new AbpValidationException("Invalid receipt book table filters.");
		}
	}
}
