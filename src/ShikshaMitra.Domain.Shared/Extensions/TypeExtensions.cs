using System;
using System.Text.RegularExpressions;

namespace ShikshaMitra.Extensions;

public static partial class TypeExtensions
{
	public static string ToEntityNameFromManager(this Type type)
	{
		// Remove the "Manager" suffix
		string nameWithoutManager = type.Name.Replace("Manager", string.Empty);

		// Split the class name into words based on uppercase letters and join them with spaces
		string entityName = SplitCamelCaseRegex().Replace(nameWithoutManager, "$1 $2");

		// Convert to lowercase
		return entityName.ToLowerInvariant();
	}

	/// <summary>
	/// Splits a camel case string into words.
	/// </summary>
	/// <returns></returns>
	[GeneratedRegex("([a-z])([A-Z])")]
	private static partial Regex SplitCamelCaseRegex();
}