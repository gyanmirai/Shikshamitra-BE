using System;
using System.Linq;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Students;

namespace ShikshaMitra.Finance;

public static class FinanceStudentScopeQueryBuilder
{
	public static IQueryable<Student> ApplyClassDivisionFilter(
		this IQueryable<Student> query,
		IFinanceClassDivisionFilter? filter)
	{
		if (filter == null)
		{
			return query;
		}

		FinanceClassDivisionFilterValidator.Normalize(filter);

		if (filter.StandardId.HasValue)
		{
			query = query.Where(s => s.StandardId == filter.StandardId.Value);
		}

		if (filter.DivisionId.HasValue)
		{
			query = query.Where(s => s.DivisionId == filter.DivisionId.Value);
		}

		return query;
	}
}
