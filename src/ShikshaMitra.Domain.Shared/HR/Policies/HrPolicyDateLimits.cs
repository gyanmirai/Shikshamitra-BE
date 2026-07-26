namespace ShikshaMitra.HR.Policies;

public static class HrPolicyDateLimits
{
	public const int MinimumYear = 1900;
	public const int MaximumYear = 2100;

	public static bool IsSupportedYear(int year) =>
		year is >= MinimumYear and <= MaximumYear;
}
