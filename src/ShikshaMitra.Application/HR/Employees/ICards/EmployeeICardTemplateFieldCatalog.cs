using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Employees.ICards;

internal static class EmployeeICardTemplateFieldCatalog
{
	public static readonly HashSet<string> SupportedFieldNames = new(StringComparer.OrdinalIgnoreCase)
	{
		"photo",
		"name",
		"employeeCode",
		"designation",
		"department",
		"organization",
		"contactNo",
		"bloodGroup",
		"validityYear",
		"address"
	};

	public static readonly HashSet<string> MandatoryFieldNames = new(StringComparer.OrdinalIgnoreCase)
	{
		"photo",
		"name",
		"employeeCode"
	};

	public static Dictionary<string, bool> BuildDefaultFields()
	{
		return SupportedFieldNames.ToDictionary(
			fieldName => fieldName,
			fieldName => MandatoryFieldNames.Contains(fieldName),
			StringComparer.OrdinalIgnoreCase);
	}

	public static Dictionary<string, bool> BuildDisabledFields()
	{
		return SupportedFieldNames.ToDictionary(
			fieldName => fieldName,
			_ => false,
			StringComparer.OrdinalIgnoreCase);
	}
}
