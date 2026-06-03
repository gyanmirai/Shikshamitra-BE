using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.SalarySlips;

namespace ShikshaMitra.HR.Payroll.Settings;

internal static class PayrollComponentImpactProjection
{
	public static IReadOnlyList<PayrollComponentImpactEmployeeDto> BuildEmployeeSamples(
		IReadOnlyList<EmployeePayrollComponent> rows,
		IReadOnlyDictionary<Guid, string> employeeNames,
		bool includeEmployees,
		int skipCount,
		int maxResultCount)
	{
		var take = includeEmployees ? maxResultCount : Math.Min(maxResultCount, 5);
		return rows
			.GroupBy(x => x.EmployeeId)
			.Skip(skipCount)
			.Take(take)
			.Select(group =>
			{
				var row = group.First();
				return new PayrollComponentImpactEmployeeDto
				{
					EmployeeId = row.EmployeeId,
					EmployeeName = employeeNames.TryGetValue(row.EmployeeId, out var name) ? name : row.EmployeeId.ToString(),
					CurrentAmount = row.Amount,
					UsesMasterValue = row.UsesMasterValue
				};
			})
			.ToList();
	}

	public static SalaryComponentDependency? MapDependency(ComponentDependency dependOn)
	{
		return dependOn switch
		{
			ComponentDependency.None => SalaryComponentDependency.None,
			ComponentDependency.Basic => SalaryComponentDependency.Basic,
			ComponentDependency.Gross => SalaryComponentDependency.Gross,
			_ => null
		};
	}

	public static string ResolveEffectiveMonth(string? effectiveMonth, DateTime now)
	{
		if (DateTime.TryParse($"{effectiveMonth}-01", out var parsed))
		{
			return parsed.ToString("yyyy-MM");
		}

		return now.AddMonths(1).ToString("yyyy-MM");
	}

	public static bool IsFutureEffectiveMonth(string? effectiveMonth, DateTime now)
	{
		if (string.IsNullOrWhiteSpace(effectiveMonth))
		{
			return false;
		}

		return string.Compare(effectiveMonth, now.ToString("yyyy-MM"), StringComparison.Ordinal) > 0;
	}

	public static string BuildActiveCacheKey(string cachePrefix, Guid? tenantId, int skipCount, int maxResultCount) =>
		$"{cachePrefix}:Active:Tenant:{tenantId ?? Guid.Empty}:Skip:{skipCount}:Take:{maxResultCount}";

	public static IReadOnlyList<string> BuildCommonActiveCacheKeys(string cachePrefix, Guid? tenantId)
	{
		var cacheKeysToRemove = new List<string>();
		int[] commonPageSizes = [10, 20, 25, 50, 100];
		const int maxPages = 10;

		foreach (var pageSize in commonPageSizes)
		{
			for (var page = 0; page < maxPages; page++)
			{
				var skipCount = page * pageSize;
				cacheKeysToRemove.Add(BuildActiveCacheKey(cachePrefix, tenantId, skipCount, pageSize));
			}
		}

		return cacheKeysToRemove;
	}

	public static bool IsNameUniqueViolation(Exception exception, string indexName, string tableHint)
	{
		for (var ex = exception; ex != null; ex = ex.InnerException!)
		{
			var message = ex.Message;
			if (string.IsNullOrEmpty(message))
			{
				continue;
			}

			if (message.Contains(indexName, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (message.Contains(tableHint, StringComparison.OrdinalIgnoreCase)
			    && message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (message.Contains("23505", StringComparison.Ordinal)
			    && (message.Contains(tableHint, StringComparison.OrdinalIgnoreCase)
			        || message.Contains(indexName, StringComparison.OrdinalIgnoreCase)))
			{
				return true;
			}
		}

		return false;
	}

	public static decimal ResolveComponentAmount(CalculationType calculationType, decimal? amount, decimal? percentage, decimal fallbackAmount)
	{
		return calculationType == CalculationType.Amount
			? amount ?? fallbackAmount
			: percentage ?? fallbackAmount;
	}

}
