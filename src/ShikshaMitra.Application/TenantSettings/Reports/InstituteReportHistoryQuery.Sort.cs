using System;
using System.Linq;
using System.Linq.Expressions;

namespace ShikshaMitra.TenantSettings.Reports;

internal static partial class InstituteReportHistoryQuery
{
	public static IOrderedQueryable<InstituteReportHistoryRow> Sort(IQueryable<InstituteReportHistoryRow> query, string? sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting)) return query.OrderByDescending(x => x.Job.CreationTime).ThenByDescending(x => x.Job.Id);
		if (sorting.Length > 500) throw Invalid();
		var parts = sorting.Split(','); if (parts.Length > 5) throw Invalid();
		var ordered = false;
		foreach (var part in parts)
		{
			var tokens = part.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (tokens.Length != 2 || tokens[1] is not ("asc" or "desc")) throw Invalid();
			var row = Expression.Parameter(typeof(InstituteReportHistoryRow), "row");
			Expression field;
			if (TextColumns.Contains(tokens[0]))
			{
				field = Text(row, tokens[0]);
				query = Order(query, row, Expression.Equal(field, Expression.Constant(null, typeof(string))), tokens[1] == "desc", ordered);
				ordered = true;
			}
			else if (DateColumns.Contains(tokens[0])) field = Expression.Coalesce(Date(row, tokens[0]), Expression.Constant(DateTime.UnixEpoch));
			else throw Invalid();
			query = Order(query, row, field, tokens[1] == "desc", ordered); ordered = true;
		}
		return ((IOrderedQueryable<InstituteReportHistoryRow>)query).ThenByDescending(x => x.Job.CreationTime).ThenByDescending(x => x.Job.Id);
	}
	private static IQueryable<InstituteReportHistoryRow> Order(IQueryable<InstituteReportHistoryRow> query, ParameterExpression row, Expression key, bool descending, bool then)
	{
		var name = (then ? "ThenBy" : "OrderBy") + (descending ? "Descending" : "");
		return query.Provider.CreateQuery<InstituteReportHistoryRow>(Expression.Call(typeof(Queryable), name,
			[typeof(InstituteReportHistoryRow), key.Type], query.Expression, Expression.Quote(Expression.Lambda(key, row))));
	}
}
