using System;
using ShikshaMitra.Common;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionStudentPhoto
{
	internal const int MaxBytes = 2 * 1024 * 1024;
	public static (byte[] Content, string Extension) Decode(string value)
	{
		var extension = value.StartsWith("data:image/jpeg;base64,", StringComparison.Ordinal) ? ".jpg" :
			value.StartsWith("data:image/png;base64,", StringComparison.Ordinal) ? ".png" : null;
		var comma = value.IndexOf(',');
		if (extension == null || comma < 0 || value.Length - comma - 1 > ((MaxBytes + 2) / 3) * 4)
			throw new BusinessException("Admissions:InvalidStudentPhoto");
		byte[] bytes;
		try { bytes = Convert.FromBase64String(value[(comma + 1)..]); }
		catch (FormatException exception) { throw new BusinessException("Admissions:InvalidStudentPhoto", innerException: exception); }
		if (bytes.Length == 0 || bytes.Length > MaxBytes) throw new BusinessException("Admissions:InvalidStudentPhoto");
		FileValidationService.ValidateProfilePictureUpload("photo" + extension, bytes);
		FileValidationService.ValidateDocumentFileSignature(bytes, extension);
		return (bytes, extension);
	}
}
