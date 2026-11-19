using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using Volo.Abp.Validation;

namespace ShikshaMitra.Finance.Common;

// Settings expose text and binary enum columns only. Keep the wire contract local;
// FinanceTableQuery owns predicate construction and same-column OR semantics.
internal sealed class SettingsTableFilter<T>(IReadOnlyDictionary<string, LambdaExpression> fields)
{
	public FinanceTableQuery Parse(string? filters)
	{
		try
		{
			if (!string.IsNullOrWhiteSpace(filters))
			{
				if (filters.Length > 16000) throw new FormatException();
				using var document = JsonDocument.Parse(filters);
				if (document.RootElement.ValueKind != JsonValueKind.Array) throw new FormatException();
				foreach (var item in document.RootElement.EnumerateArray())
				{
					if (item.ValueKind != JsonValueKind.Object ||
						!item.TryGetProperty("column", out var column) || column.ValueKind != JsonValueKind.String ||
						string.IsNullOrEmpty(column.GetString())) throw new FormatException();
				}
			}
			return FinanceTableQuery.Parse(filters, null, null, null, fields.ToDictionary(x => x.Key,
				x => x.Value.ReturnType == typeof(string)
					? new FinanceTableColumn(FinanceTableValueType.Text, false)
					: new FinanceTableColumn(FinanceTableValueType.Enum, false, [0, 1])));
		}
		catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
		{
			throw new AbpValidationException("Invalid settings table filters.");
		}
	}

	public IQueryable<T> Apply(IQueryable<T> rows, FinanceTableQuery query) => query.Apply(rows, fields);
}
