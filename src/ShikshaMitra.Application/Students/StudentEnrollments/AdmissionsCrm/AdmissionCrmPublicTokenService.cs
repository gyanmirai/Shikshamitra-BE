using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Security.Encryption;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicTokenService(IStringEncryptionService encryption) : ITransientDependency
{
	public string CreateRawToken()
	{
		var bytes = RandomNumberGenerator.GetBytes(32);
		return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
	}

	public string EncryptRequired(string value) => encryption.Encrypt(value) ??
		throw new BusinessException("AdmissionCrm:EncryptionFailed");

	public string DecryptRequired(string value)
	{
		var decrypted = encryption.Decrypt(value);
		return string.IsNullOrWhiteSpace(decrypted)
			? throw new BusinessException("AdmissionCrm:DecryptionFailed")
			: decrypted;
	}

	public static string CreateOtpCode() =>
		RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

	public static string HashSecret(string value)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()));
		return Convert.ToHexString(hash).ToLowerInvariant();
	}

	public static string CreateStatusPath(Guid tenantId, string token) =>
		$"/student/admissions/status/{tenantId:D}/{token}";

	public static string MaskDestination(AdmissionInquiryLead lead)
	{
		if (!string.IsNullOrWhiteSpace(lead.PrimaryContactNo))
		{
			var digits = new string(lead.PrimaryContactNo.Where(char.IsDigit).ToArray());
			return "phone ending " + (digits.Length <= 4 ? digits : digits[^4..]);
		}

		if (!string.IsNullOrWhiteSpace(lead.Email))
		{
			var parts = lead.Email.Split('@', 2);
			if (parts.Length == 2)
			{
				var prefix = parts[0].Length <= 2 ? parts[0] : parts[0][..2] + "***";
				return $"{prefix}@{parts[1]}";
			}
		}

		return "registered contact";
	}
}
