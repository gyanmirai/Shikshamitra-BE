using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Volo.Abp.Linq;

namespace ShikshaMitra.TenantSettings.Reports;

internal static class InstituteReportQuery
{
	public static async Task<(List<T> Items, int Count)?> PageAsync<T>(IQueryable<T> source, InstituteReportRequest request, bool all,
		IAsyncQueryableExecuter executor, Func<IQueryable<T>, IOrderedQueryable<T>> order,
		Dictionary<string, Expression<Func<T, string?>>> text, Dictionary<string, Expression<Func<T, decimal?>>>? numbers = null)
	{
		numbers ??= [];
		// Database case folding and collation do not preserve OrdinalIgnoreCase for every school name.
		// Keep textual and formatted/derived criteria on the established projector until they have an equivalent database contract.
		if (request.SortField == "sizeBytes" || request.Conditions.Any(x => text.ContainsKey(x.Field) || !numbers.ContainsKey(x.Field) || x.Operator == "contains")
			|| (request.SortField != null && (text.ContainsKey(request.SortField) || !numbers.ContainsKey(request.SortField)))) return null;
		foreach (var condition in request.Conditions)
		{
			if (!decimal.TryParse(condition.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var expected)) { source = source.Where(_ => false); continue; }
			var field = numbers[condition.Field];
			var match = IntegerPredicate(field.Body, expected, condition.Operator);
			if (match == null) return null;
			source = source.Where(Expression.Lambda<Func<T, bool>>(match, field.Parameters));
		}
		var count = await executor.CountAsync(source);
		IQueryable<T> sorted = order(source);
		if (request.SortField != null)
		{
			var field = numbers[request.SortField];
			var key = Expression.Lambda<Func<T, double>>(Expression.Convert(Expression.Coalesce(field.Body, Expression.Constant(decimal.MinValue)), typeof(double)), field.Parameters);
			var primary = request.Descending ? source.OrderByDescending(key) : source.OrderBy(key);
			sorted = AppendDefaultOrder(primary, order(source));
		}
		if (!all) sorted = sorted.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize);
		return (await executor.ToListAsync(sorted), count);
	}
	private static Expression? IntegerPredicate(Expression field, decimal expected, string op)
	{
		// Keep integer comparisons numeric: SQLite otherwise compares decimal casts as formatted text.
		while (field is UnaryExpression { NodeType: ExpressionType.Convert } conversion) field = conversion.Operand;
		var type = Nullable.GetUnderlyingType(field.Type) ?? field.Type;
		if (type != typeof(int) && type != typeof(long)) return null;
		var minimum = type == typeof(int) ? int.MinValue : (decimal)long.MinValue;
		var maximum = type == typeof(int) ? int.MaxValue : (decimal)long.MaxValue;
		var present = Nullable.GetUnderlyingType(field.Type) == null ? (Expression)Expression.Constant(true) : Expression.NotEqual(field, Expression.Constant(null, field.Type));
		if (op == "eq" && (expected != decimal.Truncate(expected) || expected < minimum || expected > maximum)) return Expression.Constant(false);
		if (op == "gt" && expected < minimum || op == "lt" && expected > maximum) return present;
		if (op == "gt" && expected >= maximum || op == "lt" && expected <= minimum) return Expression.Constant(false);
		var integral = op == "gt" ? decimal.Floor(expected) : op == "lt" ? decimal.Ceiling(expected) : expected;
		Expression value = Expression.Constant(Convert.ChangeType(integral, type, CultureInfo.InvariantCulture), type);
		if (field.Type != type) value = Expression.Convert(value, field.Type);
		return op switch { "gt" => Expression.GreaterThan(field, value), "lt" => Expression.LessThan(field, value), _ => Expression.Equal(field, value) };
	}
	private static IOrderedQueryable<T> AppendDefaultOrder<T>(IOrderedQueryable<T> primary, IOrderedQueryable<T> fallback)
	{
		var selectors = new Stack<MethodCallExpression>();
		for (Expression node = fallback.Expression; node is MethodCallExpression call
			&& call.Method.DeclaringType == typeof(Queryable) && call.Method.Name is "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending"; node = call.Arguments[0])
			selectors.Push(call);
		foreach (var selector in selectors)
		{
			var name = selector.Method.Name.EndsWith("Descending", StringComparison.Ordinal) ? "ThenByDescending" : "ThenBy";
			primary = (IOrderedQueryable<T>)primary.Provider.CreateQuery<T>(Expression.Call(typeof(Queryable), name,
				selector.Method.GetGenericArguments(), primary.Expression, selector.Arguments[1]));
		}
		return primary;
	}
}
