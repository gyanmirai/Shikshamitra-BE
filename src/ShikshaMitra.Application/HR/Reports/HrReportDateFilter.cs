using System;
using System.Linq;
using System.Linq.Expressions;

namespace ShikshaMitra.HR.Reports;

internal static class HrReportDateFilter
{
	public static IQueryable<T> ApplyDateOnlyRange<T>(
		IQueryable<T> query,
		HrReportFilterDto filters,
		Expression<Func<T, DateOnly>> selector)
	{
		if (filters.FromDate.HasValue)
		{
			query = query.Where(BuildDateComparison(selector, filters.FromDate.Value, true));
		}

		if (filters.ToDate.HasValue)
		{
			query = query.Where(BuildDateComparison(selector, filters.ToDate.Value, false));
		}

		return query;
	}

	private static Expression<Func<T, bool>> BuildDateComparison<T>(
		Expression<Func<T, DateOnly>> selector,
		DateOnly value,
		bool gte)
	{
		var comparison = gte
			? Expression.GreaterThanOrEqual(selector.Body, Expression.Constant(value))
			: Expression.LessThanOrEqual(selector.Body, Expression.Constant(value));
		return Expression.Lambda<Func<T, bool>>(comparison, selector.Parameters);
	}
}
