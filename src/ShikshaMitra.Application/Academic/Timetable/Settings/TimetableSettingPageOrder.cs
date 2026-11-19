using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ShikshaMitra.Academic.Timetable.Settings;

internal static class TimetableSettingPageOrder
{
	internal static IOrderedQueryable<TEntity> ByStandardName<TEntity>(IQueryable<TEntity> query,
		IReadOnlyDictionary<Guid, string> standardNames, Expression<Func<TEntity, Guid>> standardId)
	{
		// Preserve the existing CLR name comparer rather than adopting database collation.
		var ranks = standardNames.Values.Distinct(StringComparer.CurrentCulture).OrderBy(name => name, StringComparer.CurrentCulture)
			.Select((name, rank) => new { name, rank }).ToDictionary(item => item.name, item => item.rank, StringComparer.CurrentCulture);
		Expression expression = Expression.Constant(ranks.Count);
		foreach (var pair in standardNames)
			expression = Expression.Condition(Expression.Equal(standardId.Body, Expression.Constant(pair.Key)),
				Expression.Constant(ranks[pair.Value]), expression);
		return query.OrderBy(Expression.Lambda<Func<TEntity, int>>(expression, standardId.Parameters));
	}
}
