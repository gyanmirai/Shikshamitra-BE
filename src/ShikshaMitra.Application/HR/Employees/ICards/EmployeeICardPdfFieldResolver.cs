using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Employees.ICards;

internal static class EmployeeICardPdfFieldResolver
{
	public static string? ResolveFieldValue(
		EmployeeICardTemplate template,
		Employee employee,
		string? organization,
		string? fieldKey,
		string? fallback)
	{
		var address = string.Join(", ", new[] { employee.Address1, employee.Address2, employee.Address3 }.Where(x => !string.IsNullOrWhiteSpace(x)));
		var employeeName = string.IsNullOrWhiteSpace(employee.EmployeeName) ? employee.ShortName ?? "Staff" : employee.EmployeeName;
		return fieldKey?.ToLowerInvariant() switch
		{
			"name" => employeeName,
			"employeecode" => employee.EmployeeCode,
			"designation" => employee.Designation,
			"department" => employee.Department,
			"organization" => organization,
			"contactno" => employee.MobileNo,
			"bloodgroup" => employee.BloodGroup,
			"validityyear" => template.Name,
			"address" => address,
			"schoolname" => string.IsNullOrWhiteSpace(template.SchoolName) ? template.Name : template.SchoolName,
			"schooladdress" => string.IsNullOrWhiteSpace(template.SchoolAddress) ? organization ?? "School" : template.SchoolAddress,
			"static" => fallback,
			_ => fallback
		};
	}

	public static string BuildIdentityPayload(Employee employee) =>
		$"employee:{employee.Id:N}:{employee.EmployeeCode ?? employee.EmployeeName ?? "unknown"}";

	public static string? ResolveOrganizationName(
		Employee employee,
		IReadOnlyDictionary<Guid, List<EmployeeOrganizationDto>> organizationsByUserId)
	{
		return employee.UserId.HasValue &&
			organizationsByUserId.TryGetValue(employee.UserId.Value, out var organizations)
				? organizations.FirstOrDefault()?.Name
				: null;
	}

	public static bool IsFieldEnabled(IReadOnlyDictionary<string, bool> fields, string fieldName) =>
		fields.TryGetValue(fieldName, out var enabled) && enabled;
}
