using System;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Cache key provider for employee-related cache items.
/// </summary>
public static class EmployeeCacheKeyProvider
{
	public static string GetEmployeeByIdKey(Guid employeeId) =>
		$"e:id:{employeeId}";

	public static string GetEmployeeByUserIdKey(Guid userId) =>
		$"e:uid:{userId}";

	public static string GetEmployeeByCodeKey(string employeeCode) =>
		$"e:code:{employeeCode}";

	public static string GetEmployeeAllKeysPattern(Guid employeeId) =>
		$"e:*:{employeeId}";
}
