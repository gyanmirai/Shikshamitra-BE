using System;
using System.IO;
using System.Linq;
using ShikshaMitra.Common;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmDocumentUploadValidator
{
	internal static void Validate(
		AdmissionCrmDocumentRequirementDto requirement,
		ProcessedAdmissionCrmApplicantDocumentUploadDto input,
		DateTime todayUtc)
	{
		if (input.Content.Length == 0 || input.Size != input.Content.LongLength)
		{
			throw new BusinessException("AdmissionCrm:InvalidDocumentContent");
		}

		if (string.IsNullOrWhiteSpace(input.FileName) ||
		    input.FileName.Length > AdmissionCrmConsts.MaxDocumentFileNameLength ||
		    input.FileName.IndexOfAny(['/', '\\']) >= 0 ||
		    Path.GetFileName(input.FileName) != input.FileName)
		{
			throw new BusinessException("AdmissionCrm:InvalidDocumentFileName");
		}

		var maxBytes = (long)(requirement.MaximumSizeMb * 1024 * 1024);
		if (input.Size > maxBytes)
		{
			throw new BusinessException("AdmissionCrm:DocumentFileSizeExceeded")
				.WithData("MaximumSizeMb", requirement.MaximumSizeMb);
		}

		var extension = Path.GetExtension(input.FileName).TrimStart('.').ToLowerInvariant();
		if (!requirement.AcceptedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) ||
		    !FileValidationService.IsSupportedDocumentExtension(extension))
		{
			throw new BusinessException("AdmissionCrm:DocumentFileTypeNotAccepted")
				.WithData("Extension", extension);
		}

		try
		{
			FileValidationService.ValidateDocumentFileSignature(input.Content, "." + extension);
		}
		catch (BusinessException)
		{
			throw new BusinessException("AdmissionCrm:DocumentContentMismatch")
				.WithData("Extension", extension);
		}

		if (!FileValidationService.IsMatchingDocumentContentType(extension, input.ContentType))
		{
			throw new BusinessException("AdmissionCrm:DocumentContentTypeMismatch")
				.WithData("Extension", extension)
				.WithData("ContentType", input.ContentType);
		}

		if (requirement.ValidityDateRequired && !input.ValidUntil.HasValue)
		{
			throw new BusinessException("AdmissionCrm:DocumentValidityDateRequired");
		}

		if (input.ValidUntil.HasValue && input.ValidUntil.Value.Date < todayUtc.Date)
		{
			throw new BusinessException("AdmissionCrm:DocumentAlreadyExpired");
		}
	}
}
