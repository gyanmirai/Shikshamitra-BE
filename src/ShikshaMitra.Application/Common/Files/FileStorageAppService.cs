using System;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Common.Files;

[Volo.Abp.RemoteService(false)]
public class FileStorageAppService(
	IBlobContainer blobContainer,
	ICurrentTenant currentTenant
) : ApplicationService, IFileStorageAppService
{
	// Blob path: files/{tenantId}/{guid}{.ext}
	// Only the {guid}{.ext} part is returned to/accepted from callers.

	public async Task<string> UploadAsync(string originalFileName, byte[] fileBytes)
	{
		var ext = Path.GetExtension(originalFileName);
		var uniqueFileName = $"{Guid.NewGuid():N}{ext}";
		var blobName = BuildBlobName(uniqueFileName);

		using var stream = new MemoryStream(fileBytes);
		await blobContainer.SaveAsync(blobName, stream, overrideExisting: true);

		return uniqueFileName;
	}

	public async Task<FileDownloadResult> GetAsync(string uniqueFileName)
	{
		EnsureValidFileIdentifier(uniqueFileName);

		var blobName = BuildBlobName(uniqueFileName);
		var stream = await blobContainer.GetOrNullAsync(blobName);

		if (stream == null)
			throw new UserFriendlyException("File not found.");

		using var ms = new MemoryStream();
		await stream.CopyToAsync(ms);

		var contentType = GetContentType(Path.GetExtension(uniqueFileName));
		return new FileDownloadResult(ms.ToArray(), contentType, uniqueFileName);
	}

	public async Task DeleteAsync(string uniqueFileName)
	{
		EnsureValidFileIdentifier(uniqueFileName);
		await blobContainer.DeleteAsync(BuildBlobName(uniqueFileName));
	}

	private static void EnsureValidFileIdentifier(string uniqueFileName)
	{
		// Reject anything that looks like a path traversal.
		if (string.IsNullOrWhiteSpace(uniqueFileName) || uniqueFileName.Contains('/') ||
			uniqueFileName.Contains('\\') || uniqueFileName.Contains(".."))
			throw new UserFriendlyException("Invalid file identifier.");
	}

	private string BuildBlobName(string uniqueFileName)
	{
		var tenantSegment = currentTenant.Id?.ToString() ?? "host";
		return $"files/{tenantSegment}/{uniqueFileName}";
	}

	private static string GetContentType(string ext) =>
		ext.ToLowerInvariant() switch
		{
			".png" => "image/png",
			".jpg" or ".jpeg" => "image/jpeg",
			".gif" => "image/gif",
			".webp" => "image/webp",
			".pdf" => "application/pdf",
			".doc" => "application/msword",
			".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
			".xls" => "application/vnd.ms-excel",
			".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
			".mp4" => "video/mp4",
			".mp3" => "audio/mpeg",
			_ => "application/octet-stream"
		};
}
