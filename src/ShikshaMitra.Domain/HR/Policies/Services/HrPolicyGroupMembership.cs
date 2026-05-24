using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Employees;

namespace ShikshaMitra.HR.Policies;

public static class HrPolicyGroupMembership
{
	public static bool MatchesEmployee(Group group, Employee employee)
	{
		if (group.Members.Any(member =>
			member.MemberType == GroupMemberType.Employee &&
			member.MemberId == employee.Id))
		{
			return true;
		}

		var rules = group.GetRuleBasedEmployeeRules().ToList();
		var hasDynamicEmployeeSelection = group.RuleBasedRoleIds.Count > 0 || rules.Count > 0;
		if (!hasDynamicEmployeeSelection)
		{
			return false;
		}

		if (group.RuleBasedRoleIds.Count > 0 && !group.RuleBasedRoleIds.Contains(employee.RoleId))
		{
			return false;
		}

		return rules.All(rule => MatchesRule(employee, rule));
	}

	public static List<Guid> ResolveEmployeeIds(Group group, IReadOnlyList<Employee> employees)
	{
		var ids = new HashSet<Guid>(group.Members
			.Where(member => member.MemberType == GroupMemberType.Employee)
			.Select(member => member.MemberId));

		foreach (var employee in employees.Where(employee => MatchesEmployee(group, employee)))
		{
			ids.Add(employee.Id);
		}

		return ids.ToList();
	}

	private static bool MatchesRule(Employee employee, GroupEmployeeRule rule)
	{
		var value = GetRuleValue(employee, rule.Field);
		if (value == null)
		{
			return rule.Operator == RuleOperator.NotEquals;
		}

		return MatchesOperator(value, rule.Operator, rule.Value);
	}

	private static string? GetRuleValue(Employee employee, EmployeeRuleField field) => field switch
	{
		EmployeeRuleField.Department => employee.Department,
		EmployeeRuleField.Role => employee.RoleId.ToString("D"),
		EmployeeRuleField.EmploymentType => employee.EmployeeGroup?.ToString(),
		EmployeeRuleField.Name => employee.EmployeeName,
		EmployeeRuleField.Gender => employee.GenderType.ToString(),
		_ => null
	};

	private static bool MatchesOperator(string actual, RuleOperator ruleOperator, string expected)
	{
		var normalizedActual = actual.Trim();
		var normalizedExpected = (expected ?? string.Empty).Trim();

		return ruleOperator switch
		{
			RuleOperator.Equals => EqualsIgnoreCase(normalizedActual, normalizedExpected),
			RuleOperator.NotEquals => !EqualsIgnoreCase(normalizedActual, normalizedExpected),
			RuleOperator.Contains => normalizedActual.Contains(normalizedExpected, StringComparison.OrdinalIgnoreCase),
			RuleOperator.StartsWith => normalizedActual.StartsWith(normalizedExpected, StringComparison.OrdinalIgnoreCase),
			RuleOperator.EndsWith => normalizedActual.EndsWith(normalizedExpected, StringComparison.OrdinalIgnoreCase),
			RuleOperator.In => normalizedExpected
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Any(candidate => EqualsIgnoreCase(normalizedActual, candidate)),
			RuleOperator.GreaterThan => Compare(normalizedActual, normalizedExpected) > 0,
			RuleOperator.LessThan => Compare(normalizedActual, normalizedExpected) < 0,
			RuleOperator.GreaterOrEqual => Compare(normalizedActual, normalizedExpected) >= 0,
			RuleOperator.LessOrEqual => Compare(normalizedActual, normalizedExpected) <= 0,
			_ => false
		};
	}

	private static bool EqualsIgnoreCase(string left, string right) =>
		string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

	private static int Compare(string left, string right) =>
		string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
}
