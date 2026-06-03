namespace ShikshaMitra.HR.Employees;

internal static class EmployeeIdentifierNormalizer
{
	public static string? NormalizePan(string? panNo) =>
		string.IsNullOrWhiteSpace(panNo) ? panNo : panNo.Trim().ToUpperInvariant();

	public static string? NormalizeAdhar(string? adharNo) =>
		string.IsNullOrWhiteSpace(adharNo) ? adharNo : adharNo.Trim();
}
