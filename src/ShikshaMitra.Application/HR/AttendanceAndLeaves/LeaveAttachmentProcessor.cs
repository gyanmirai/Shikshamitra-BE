using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Volo.Abp;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

internal static class LeaveAttachmentProcessor
{
	public static async Task<byte[]?> ExtractAttachmentBytesAsync(object? attachment)
	{
		if (attachment == null)
		{
			return null;
		}

		if (attachment is byte[] bytes)
		{
			return bytes;
		}

		if (attachment is Stream stream)
		{
			using var ms = new MemoryStream();
			await stream.CopyToAsync(ms);
			return ms.ToArray();
		}

		if (attachment is IFormFile formFile)
		{
			if (formFile.Length == 0 || string.IsNullOrEmpty(formFile.FileName))
			{
				return null;
			}

			ValidateFileUpload(formFile.Length, formFile.FileName);

			using var formFileStream = new MemoryStream();
			await formFile.CopyToAsync(formFileStream);
			var formFileBytes = formFileStream.ToArray();

			ValidateFileContent(formFileBytes, formFile.FileName);

			return formFileBytes;
		}

		var formFileType = attachment.GetType();
		var lengthProp = formFileType.GetProperty("Length");
		var fileNameProp = formFileType.GetProperty("FileName");
		var copyToAsyncMethod = formFileType.GetMethod("CopyToAsync");

		if (lengthProp == null || fileNameProp == null || copyToAsyncMethod == null)
		{
			return null;
		}

		var length = (long)(lengthProp.GetValue(attachment) ?? 0L);
		var fileName = (string?)fileNameProp.GetValue(attachment);

		if (length == 0 || string.IsNullOrEmpty(fileName))
		{
			return null;
		}

		ValidateFileUpload(length, fileName);

		using var memoryStream = new MemoryStream();
		await (Task)copyToAsyncMethod.Invoke(attachment,
			new object[] { memoryStream, default(System.Threading.CancellationToken) })!;
		var fileBytes = memoryStream.ToArray();

		ValidateFileContent(fileBytes, fileName);

		return fileBytes;
	}

	public static void ValidateFileUpload(long fileSize, string fileName)
	{
		const long maxSize = 5 * 1024 * 1024;
		if (fileSize > maxSize)
		{
			throw new BusinessException(HRErrorCodes.LeaveFileTooLarge);
		}

		if (fileSize == 0)
		{
			throw new BusinessException(HRErrorCodes.LeaveFileEmpty);
		}

		var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
		var extension = Path.GetExtension(fileName).ToLowerInvariant();
		if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
		{
			throw new BusinessException(HRErrorCodes.LeaveFileInvalidExtension);
		}

		if (fileName.Length > 255)
		{
			throw new BusinessException(HRErrorCodes.LeaveFileNameTooLong);
		}

		if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
		{
			throw new BusinessException(HRErrorCodes.LeaveFileInvalidCharacters);
		}
	}

	public static void ValidateFileContent(byte[] fileBytes, string fileName)
	{
		if (fileBytes.Length < 4)
		{
			throw new BusinessException(HRErrorCodes.LeaveFileTooSmall);
		}

		var extension = Path.GetExtension(fileName).ToLowerInvariant();
		var isPdf = fileBytes[0] == 0x25 && fileBytes[1] == 0x50 && fileBytes[2] == 0x44 && fileBytes[3] == 0x46;
		var isJpeg = fileBytes[0] == 0xFF && fileBytes[1] == 0xD8 && fileBytes[2] == 0xFF;
		var isPng = fileBytes[0] == 0x89 && fileBytes[1] == 0x50 && fileBytes[2] == 0x4E && fileBytes[3] == 0x47;

		var isValid = extension switch
		{
			".pdf" => isPdf,
			".jpg" or ".jpeg" => isJpeg,
			".png" => isPng,
			_ => false
		};

		if (!isValid)
		{
			throw new BusinessException(HRErrorCodes.LeaveFileContentMismatch);
		}
	}

	public static (string ContentType, string Extension) DetectContentType(byte[] fileBytes)
	{
		if (fileBytes.Length >= 4 &&
		    fileBytes[0] == 0x25 && fileBytes[1] == 0x50 && fileBytes[2] == 0x44 && fileBytes[3] == 0x46)
		{
			return ("application/pdf", ".pdf");
		}

		if (fileBytes.Length >= 3 && fileBytes[0] == 0xFF && fileBytes[1] == 0xD8 && fileBytes[2] == 0xFF)
		{
			return ("image/jpeg", ".jpg");
		}

		if (fileBytes.Length >= 4 &&
		    fileBytes[0] == 0x89 && fileBytes[1] == 0x50 && fileBytes[2] == 0x4E && fileBytes[3] == 0x47)
		{
			return ("image/png", ".png");
		}

		return ("application/octet-stream", ".bin");
	}
}
