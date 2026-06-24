using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ShikshaMitra.Common;

namespace ShikshaMitra.HR.Employees.Search;

public static class EmployeeSearchQuery
{
	public static async Task<IQueryable<Employee>> ApplyFiltersAsync(
		IQueryable<Employee> queryable,
		GetEmployeeListInput input,
		EmployeeOrganizationScopeResolver organizationScopeResolver)
	{
		if (!string.IsNullOrEmpty(input.Status) &&
			Enum.TryParse<EmployeeStatusType>(input.Status, true, out var statusType))
		{
			queryable = queryable.Where(e => e.EmployeeStatusType == statusType);
		}

		if (input.IsTeaching.HasValue)
		{
			queryable = input.IsTeaching.Value
				? queryable.Where(e => e.EmployeeGroup == EmployeeGroupType.Teaching)
				: queryable.Where(e => e.EmployeeGroup != EmployeeGroupType.Teaching);
		}

		if (input.RoleId.HasValue)
		{
			queryable = queryable.Where(e => e.RoleId == input.RoleId.Value);
		}

		if (input.MediumStageId.HasValue)
		{
			var matchingUserIds = await organizationScopeResolver.GetUserIdsWithAnyStageAsync([input.MediumStageId.Value]);
			queryable = queryable.Where(e => e.UserId.HasValue && matchingUserIds.Contains(e.UserId.Value));
		}

		if (!string.IsNullOrWhiteSpace(input.Designation))
		{
			var designation = input.Designation.Trim().ToLower();
			queryable = queryable.Where(e => e.Designation != null && e.Designation.ToLower().Contains(designation));
		}

		return ApplySearchFilter(queryable, input.Search);
	}

	private static IQueryable<Employee> ApplySearchFilter(IQueryable<Employee> queryable, string? search)
	{
		if (string.IsNullOrWhiteSpace(search))
		{
			return queryable;
		}

		var searchTerm = NormalizeSearchTerm(search);
		var tokens = SplitSearchTokens(searchTerm);
		return queryable.Where(BuildSearchPredicate(searchTerm, tokens));
	}

	private static Expression<Func<Employee, bool>> BuildSearchPredicate(string searchTerm, string[] tokens)
	{
		var employee = Expression.Parameter(typeof(Employee), "employee");
		var exactSearch = OrAny(
			ContainsLower(employee, nameof(Employee.FirstName), searchTerm),
			ContainsLower(employee, nameof(Employee.LastName), searchTerm),
			ContainsLower(employee, nameof(Employee.MiddleName), searchTerm),
			ContainsLower(employee, nameof(Employee.EmployeeCode), searchTerm),
			ContainsLower(employee, nameof(Employee.Email), searchTerm),
			ContainsRaw(employee, nameof(Employee.MobileNo), searchTerm),
			ContainsLower(employee, nameof(Employee.Designation), searchTerm),
			ContainsLower(BuildNameExpression(employee, nameof(Employee.FirstName), nameof(Employee.LastName)), searchTerm),
			ContainsLower(BuildNameExpression(employee, nameof(Employee.FirstName), nameof(Employee.MiddleName), nameof(Employee.LastName)), searchTerm));

		if (tokens.Length <= 1)
		{
			return Expression.Lambda<Func<Employee, bool>>(exactSearch, employee);
		}

		Expression allNameTokensMatch = Expression.Constant(true);
		foreach (var token in tokens)
		{
			var tokenMatchesAnyNamePart = OrAny(
				ContainsLower(employee, nameof(Employee.FirstName), token),
				ContainsLower(employee, nameof(Employee.LastName), token),
				ContainsLower(employee, nameof(Employee.MiddleName), token));
			allNameTokensMatch = Expression.AndAlso(allNameTokensMatch, tokenMatchesAnyNamePart);
		}

		return Expression.Lambda<Func<Employee, bool>>(
			Expression.OrElse(exactSearch, allNameTokensMatch),
			employee);
	}

	private static string NormalizeSearchTerm(string search) =>
		string.Join(" ", search.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

	private static string[] SplitSearchTokens(string searchTerm) =>
		searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

	private static Expression OrAny(params Expression[] expressions) =>
		expressions.Aggregate(Expression.OrElse);

	private static Expression ContainsLower(ParameterExpression parameter, string propertyName, string searchTerm) =>
		ContainsLower(Expression.Property(parameter, propertyName), searchTerm, requireNotNull: true);

	private static Expression ContainsRaw(ParameterExpression parameter, string propertyName, string searchTerm)
	{
		var property = Expression.Property(parameter, propertyName);
		return Expression.AndAlso(
			Expression.NotEqual(property, Expression.Constant(null, typeof(string))),
			Expression.Call(
				property,
				typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!,
				Expression.Constant(searchTerm)));
	}

	private static Expression ContainsLower(Expression value, string searchTerm, bool requireNotNull = false)
	{
		var contains = Expression.Call(
			Expression.Call(value, typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!),
			typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!,
			Expression.Constant(searchTerm));

		return requireNotNull
			? Expression.AndAlso(Expression.NotEqual(value, Expression.Constant(null, typeof(string))), contains)
			: contains;
	}

	private static Expression BuildNameExpression(ParameterExpression parameter, params string[] propertyNames)
	{
		var parts = propertyNames
			.Select(name => Expression.Coalesce(
				Expression.Property(parameter, name),
				Expression.Constant(string.Empty)))
			.ToArray<Expression>();
		var separator = Expression.Constant(" ");
		var result = parts[0];

		foreach (var part in parts.Skip(1))
		{
			result = Concat(Concat(result, separator), part);
		}

		return result;
	}

	private static Expression Concat(Expression left, Expression right) =>
		Expression.Call(
			typeof(string).GetMethod(nameof(string.Concat), [typeof(string), typeof(string)])!,
			left,
			right);
}
