using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionInquiryPhoneValidator
{
	public static bool IsValidIndianMobile(string? phone)
	{
		var normalized = string.Concat((phone ?? string.Empty).Where(char.IsDigit));
		return normalized.Length == 10 && normalized[0] >= '6' && normalized[0] <= '9';
	}

	public static string NormalizeAndValidateIndianMobile(string phone)
	{
		var normalized = string.Concat((phone ?? string.Empty).Where(char.IsDigit));
		if (!IsValidIndianMobile(normalized))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput,
				"Enter a valid 10-digit Indian guardian mobile number.");

		return normalized;
	}
}
