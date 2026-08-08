using System.Linq;
using System.Net.Mail;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public static class AdmissionCrmContactRules
{
	public const int MinimumPhoneDigits = 7;
	public const int MaximumPhoneDigits = 15;

	public static bool IsValidPhone(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return true;
		}

		var trimmed = value.Trim();
		if (trimmed.Length > AdmissionCrmConsts.MaxContactLength)
		{
			return false;
		}

		var parenthesisDepth = 0;
		for (var index = 0; index < trimmed.Length; index++)
		{
			var character = trimmed[index];
			if (IsAsciiDigit(character) || character is ' ' or '-' or '.')
			{
				continue;
			}

			if (character == '+' && index == 0)
			{
				continue;
			}

			if (character == '(' && parenthesisDepth == 0)
			{
				parenthesisDepth = 1;
				continue;
			}

			if (character == ')' && parenthesisDepth == 1)
			{
				parenthesisDepth = 0;
				continue;
			}

			return false;
		}

		var digitCount = trimmed.Count(IsAsciiDigit);
		return parenthesisDepth == 0 &&
		       digitCount >= MinimumPhoneDigits &&
		       digitCount <= MaximumPhoneDigits;
	}

	public static bool IsValidEmail(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return true;
		}

		var trimmed = value.Trim();
		return trimmed.Length <= AdmissionCrmConsts.MaxEmailLength &&
		       MailAddress.TryCreate(trimmed, out var parsed) &&
		       string.Equals(parsed.Address, trimmed, System.StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsAsciiDigit(char character) => character is >= '0' and <= '9';
}
