using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
namespace ShikshaMitra.HR.Reports;
internal static class HrReportEmployeeQueryFilters
{
	public static IQueryable<Employee> ApplyConditionFilters(IQueryable<Employee> query, List<HrReportFilterConditionDto>? conditions)
	{
		if (conditions == null || conditions.Count == 0)
		{
			return query;
		}
		foreach (var condition in conditions.Where(x => !string.IsNullOrWhiteSpace(x.Field)))
		{
			query = condition.Field switch
			{
				"employeeCode" => ApplyStringCondition(query, e => e.EmployeeCode, condition),
				"employeeName" => ApplyStringCondition(query, e => e.FirstName + " " + e.LastName, condition),
				"department" => ApplyStringCondition(query, e => e.Department, condition),
				"designation" => ApplyStringCondition(query, e => e.Designation, condition),
				"mobileNo" => ApplyStringCondition(query, e => e.MobileNo, condition),
				"email" => ApplyStringCondition(query, e => e.Email, condition),
				"status" => ApplyEnumCondition(query, e => e.EmployeeStatusType, condition),
				"gender" => ApplyEnumCondition(query, e => e.GenderType, condition),
				"dateOfJoining" => ApplyDateOnlyCondition(query, e => e.DateOfJoining, condition),
				"dateOfBirth" => ApplyDateOnlyCondition(query, e => e.DateOfBirth, condition),
				_ => query
			};
		}
		return query;
	}
	public static IQueryable<Employee> ApplySorts(IQueryable<Employee> query, List<HrReportSortDto>? sorts)
	{
		var firstSort = sorts?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Field));
		if (firstSort == null)
		{
			return query.OrderBy(e => e.EmployeeCode);
		}
		var desc = string.Equals(firstSort.Direction, "desc", StringComparison.OrdinalIgnoreCase);
		return firstSort.Field switch
		{
			"employeeName" => desc ? query.OrderByDescending(e => e.FirstName).ThenByDescending(e => e.LastName) : query.OrderBy(e => e.FirstName).ThenBy(e => e.LastName),
			"department" => desc ? query.OrderByDescending(e => e.Department).ThenBy(e => e.EmployeeCode) : query.OrderBy(e => e.Department).ThenBy(e => e.EmployeeCode),
			"designation" => desc ? query.OrderByDescending(e => e.Designation).ThenBy(e => e.EmployeeCode) : query.OrderBy(e => e.Designation).ThenBy(e => e.EmployeeCode),
			"status" => desc ? query.OrderByDescending(e => e.EmployeeStatusType).ThenBy(e => e.EmployeeCode) : query.OrderBy(e => e.EmployeeStatusType).ThenBy(e => e.EmployeeCode),
			"gender" => desc ? query.OrderByDescending(e => e.GenderType).ThenBy(e => e.EmployeeCode) : query.OrderBy(e => e.GenderType).ThenBy(e => e.EmployeeCode),
			"dateOfJoining" => desc ? query.OrderByDescending(e => e.DateOfJoining).ThenBy(e => e.EmployeeCode) : query.OrderBy(e => e.DateOfJoining).ThenBy(e => e.EmployeeCode),
			"dateOfBirth" => desc ? query.OrderByDescending(e => e.DateOfBirth).ThenBy(e => e.EmployeeCode) : query.OrderBy(e => e.DateOfBirth).ThenBy(e => e.EmployeeCode),
			"email" => desc ? query.OrderByDescending(e => e.Email).ThenBy(e => e.EmployeeCode) : query.OrderBy(e => e.Email).ThenBy(e => e.EmployeeCode),
			"mobileNo" => desc ? query.OrderByDescending(e => e.MobileNo).ThenBy(e => e.EmployeeCode) : query.OrderBy(e => e.MobileNo).ThenBy(e => e.EmployeeCode),
			_ => desc ? query.OrderByDescending(e => e.EmployeeCode) : query.OrderBy(e => e.EmployeeCode)
		};
	}
	public static IQueryable<T> ApplyGroupSorts<T>(IQueryable<T> query, List<HrReportSortDto>? sorts, string groupField)
	{
		var firstSort = sorts?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Field));
		if (firstSort == null)
		{
			return query.OrderBy("RowCount descending, Label ascending");
		}
		var desc = string.Equals(firstSort.Direction, "desc", StringComparison.OrdinalIgnoreCase);
		if (firstSort.Field == "rowCount")
		{
			return query.OrderBy($"RowCount {(desc ? "descending" : "ascending")}, Label ascending");
		}
		return firstSort.Field == groupField
			? query.OrderBy($"Label {(desc ? "descending" : "ascending")}")
			: query.OrderBy("RowCount descending, Label ascending");
	}
	public static IQueryable<Employee> ApplyDateRange(IQueryable<Employee> query, HrReportFilterDto filters)
	{
		if (!filters.FromDate.HasValue && !filters.ToDate.HasValue)
		{
			return query;
		}

		var dateField = string.IsNullOrWhiteSpace(filters.DateField) ? "dateOfJoining" : filters.DateField;
		return dateField switch
		{
			"dateOfBirth" => ApplyNullableDateRange(query, filters, e => e.DateOfBirth),
			_ => ApplyNullableDateRange(query, filters, e => e.DateOfJoining)
		};
	}

	private static IQueryable<Employee> ApplyStringCondition(IQueryable<Employee> query, Expression<Func<Employee, string?>> selector, HrReportFilterConditionDto condition)
	{
		var value = condition.Value ?? string.Empty;
		if (condition.Operator == HrReportFilterOperator.Contains)
		{
			return query.Where(BuildStringContains(selector, value));
		}

		if (condition.Operator == HrReportFilterOperator.In)
		{
			var values = GetConditionValues(condition);
			return values.Count == 0 ? query : query.Where(BuildStringIn(selector, values));
		}

		return query.Where(BuildStringEquals(selector, value));
	}

	private static IQueryable<Employee> ApplyEnumCondition<TEnum>(IQueryable<Employee> query, Expression<Func<Employee, TEnum?>> selector, HrReportFilterConditionDto condition)
		where TEnum : struct, Enum
	{
		var values = condition.Operator == HrReportFilterOperator.In ? GetConditionValues(condition) : [condition.Value ?? string.Empty];
		var parsed = values
			.Select(value => Enum.TryParse<TEnum>(value, true, out var result) ? result : (TEnum?)null)
			.Where(value => value.HasValue)
			.Select(value => value!.Value)
			.ToList();
		return parsed.Count == 0 ? query : query.Where(BuildNullableEnumIn(selector, parsed));
	}

	private static List<string> GetConditionValues(HrReportFilterConditionDto condition)
	{
		var values = (condition.Values ?? [])
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => x.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		if (values.Count == 0 && !string.IsNullOrWhiteSpace(condition.Value))
		{
			values.Add(condition.Value.Trim());
		}

		return values;
	}

	private static IQueryable<Employee> ApplyDateOnlyCondition(IQueryable<Employee> query, Expression<Func<Employee, DateOnly?>> selector, HrReportFilterConditionDto condition)
	{
		if (!DateOnly.TryParse(condition.Value, CultureInfo.InvariantCulture, out var date))
		{
			return query;
		}

		return condition.Operator switch
		{
			HrReportFilterOperator.GreaterThanOrEqual => query.Where(BuildNullableDateComparison(selector, date, true)),
			HrReportFilterOperator.LessThanOrEqual => query.Where(BuildNullableDateComparison(selector, date, false)),
			_ => query.Where(BuildNullableDateEquals(selector, date))
		};
	}

	private static Expression<Func<Employee, bool>> BuildStringContains(Expression<Func<Employee, string?>> selector, string value)
	{
		var actual = Expression.Coalesce(selector.Body, Expression.Constant(string.Empty));
		var contains = Expression.Call(actual, nameof(string.Contains), Type.EmptyTypes, Expression.Constant(value));
		return Expression.Lambda<Func<Employee, bool>>(contains, selector.Parameters);
	}

	private static Expression<Func<Employee, bool>> BuildStringEquals(Expression<Func<Employee, string?>> selector, string value)
	{
		var actual = Expression.Coalesce(selector.Body, Expression.Constant(string.Empty));
		var equals = Expression.Equal(actual, Expression.Constant(value));
		return Expression.Lambda<Func<Employee, bool>>(equals, selector.Parameters);
	}

	private static Expression<Func<Employee, bool>> BuildStringIn(Expression<Func<Employee, string?>> selector, List<string> values)
	{
		var contains = Expression.Call(
			Expression.Constant(values),
			nameof(List<string>.Contains),
			Type.EmptyTypes,
			Expression.Coalesce(selector.Body, Expression.Constant(string.Empty)));
		return Expression.Lambda<Func<Employee, bool>>(contains, selector.Parameters);
	}

	private static Expression<Func<Employee, bool>> BuildNullableEnumIn<TEnum>(Expression<Func<Employee, TEnum?>> selector, List<TEnum> values)
		where TEnum : struct, Enum
	{
		var hasValue = Expression.Property(selector.Body, nameof(Nullable<TEnum>.HasValue));
		var actual = Expression.Property(selector.Body, nameof(Nullable<TEnum>.Value));
		var contains = Expression.Call(Expression.Constant(values), nameof(List<TEnum>.Contains), Type.EmptyTypes, actual);
		return Expression.Lambda<Func<Employee, bool>>(Expression.AndAlso(hasValue, contains), selector.Parameters);
	}

	private static Expression<Func<Employee, bool>> BuildNullableDateEquals(Expression<Func<Employee, DateOnly?>> selector, DateOnly value)
	{
		var hasValue = Expression.Property(selector.Body, nameof(Nullable<DateOnly>.HasValue));
		var actual = Expression.Property(selector.Body, nameof(Nullable<DateOnly>.Value));
		var comparison = Expression.Equal(actual, Expression.Constant(value));
		return Expression.Lambda<Func<Employee, bool>>(Expression.AndAlso(hasValue, comparison), selector.Parameters);
	}

	private static IQueryable<Employee> ApplyNullableDateRange(IQueryable<Employee> query, HrReportFilterDto filters, Expression<Func<Employee, DateOnly?>> selector)
	{
		if (filters.FromDate.HasValue)
		{
			query = query.Where(BuildNullableDateComparison(selector, filters.FromDate.Value, true));
		}

		if (filters.ToDate.HasValue)
		{
			query = query.Where(BuildNullableDateComparison(selector, filters.ToDate.Value, false));
		}

		return query;
	}

	private static Expression<Func<Employee, bool>> BuildNullableDateComparison(Expression<Func<Employee, DateOnly?>> selector, DateOnly value, bool gte)
	{
		var hasValue = Expression.Property(selector.Body, nameof(Nullable<DateOnly>.HasValue));
		var actual = Expression.Property(selector.Body, nameof(Nullable<DateOnly>.Value));
		var comparison = gte
			? Expression.GreaterThanOrEqual(actual, Expression.Constant(value))
			: Expression.LessThanOrEqual(actual, Expression.Constant(value));
		return Expression.Lambda<Func<Employee, bool>>(Expression.AndAlso(hasValue, comparison), selector.Parameters);
	}
}
