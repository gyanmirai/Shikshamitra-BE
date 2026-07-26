using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ShikshaMitra.HR.Employees.Onboarding;
using Volo.Abp.Validation;

namespace ShikshaMitra.Controllers.HR.Employees;

internal static class PublicEmployeeApplicationUploadReader
{
	public static async Task<EmployeeApplicationFileInput?> ToFileInputAsync(
		IFormFile? file,
		long maxBytes,
		EmployeeApplicationUploadKind kind,
		string memberName,
		string sizeMessage)
	{
		if (file == null || file.Length == 0)
		{
			return null;
		}

		if (file.Length > maxBytes)
		{
			throw new AbpValidationException(
				"Employee application upload validation failed.",
				[new ValidationResult(sizeMessage, [memberName])]);
		}

		if (!EmployeeApplicationUploadPolicy.TryNormalize(
			    file.FileName,
			    file.ContentType,
			    kind,
			    out var safeFileName,
			    out var canonicalContentType))
		{
			throw new AbpValidationException(
				"Employee application upload validation failed.",
				[
					new ValidationResult(
						$"{memberName} must have a safe filename, one of the allowed extensions ({EmployeeApplicationUploadPolicy.GetAllowedExtensions(kind)}), and a matching content type.",
						[memberName])
				]);
		}

		await using var memoryStream = new MemoryStream();
		await file.CopyToAsync(memoryStream);
		return new EmployeeApplicationFileInput
		{
			FileName = safeFileName,
			ContentType = canonicalContentType,
			Content = memoryStream.ToArray(),
			Size = file.Length
		};
	}
}
