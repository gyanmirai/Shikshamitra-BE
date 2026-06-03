using Volo.Abp;

namespace ShikshaMitra.HR.Policies;

internal static partial class HrPolicyMapping
{
	public static HrPolicyStatus ParsePolicyStatus(string value) => Normalize(value) switch
	{
		"active" => HrPolicyStatus.Active,
		"archived" => HrPolicyStatus.Archived,
		_ => HrPolicyStatus.Draft
	};

	public static string MapPolicyStatus(HrPolicyStatus value) => value switch
	{
		HrPolicyStatus.Active => "active",
		HrPolicyStatus.Archived => "archived",
		_ => "draft"
	};

	public static void EnsureActive(HrPolicyStatus status)
	{
		if (status != HrPolicyStatus.Active)
		{
			throw new BusinessException(HRErrorCodes.HrPolicyMustBeActive);
		}
	}

	private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}
