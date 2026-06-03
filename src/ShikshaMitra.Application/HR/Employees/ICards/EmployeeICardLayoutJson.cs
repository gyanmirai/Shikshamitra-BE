using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShikshaMitra.HR.Employees.ICards;

internal static class EmployeeICardLayoutJson
{
	public static readonly JsonSerializerOptions Options = new()
	{
		PropertyNameCaseInsensitive = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};
}
