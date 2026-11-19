using System;
using System.IO;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;

namespace ShikshaMitra.HR.Employees.ICards;

[RemoteService(false)]
public class EmployeeICardAssetService(
	IBlobContainer blobContainer,
	IGuidGenerator guidGenerator) : ApplicationService, ITransientDependency
{
	public async Task<EmployeeICardAssetUploadResult> UploadAssetAsync(string fileName, string contentType, byte[] content)
	{
		EmployeeICardTemplateRules.ValidateAsset(fileName, contentType, content);

		var sanitizedFileName = Path.GetFileName(fileName);
		var blobName = $"staff-icards/assets/{CurrentTenant.Id ?? Guid.Empty}/{guidGenerator.Create()}_{sanitizedFileName}";
		await blobContainer.SaveAsync(blobName, content, overrideExisting: true);

		return new EmployeeICardAssetUploadResult
		{
			BlobName = blobName,
			FileName = sanitizedFileName,
			ContentType = contentType,
			Size = content.LongLength
		};
	}

	public async Task<EmployeeICardAssetDownloadResult> DownloadAssetAsync(string blobName)
	{
		EmployeeICardTemplateRules.ValidateAssetBlobName(blobName, CurrentTenant.Id);
		var bytes = await GetRequiredBytesAsync(blobName);

		return new EmployeeICardAssetDownloadResult
		{
			Content = bytes,
			FileName = Path.GetFileName(blobName),
			ContentType = GetImageContentType(blobName)
		};
	}

	public async Task<byte[]?> TryGetBytesAsync(string? blobName)
	{
		if (string.IsNullOrWhiteSpace(blobName))
		{
			return null;
		}

		try
		{
			await using var blobStream = await blobContainer.GetAsync(blobName);
			await using var memoryStream = new MemoryStream();
			await blobStream.CopyToAsync(memoryStream);
			return memoryStream.ToArray();
		}
		catch
		{
			return null;
		}
	}

	private async Task<byte[]> GetRequiredBytesAsync(string blobName)
	{
		try
		{
			await using var blobStream = await blobContainer.GetAsync(blobName);
			await using var memoryStream = new MemoryStream();
			await blobStream.CopyToAsync(memoryStream);
			return memoryStream.ToArray();
		}
		catch
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardAssetBlobInvalid);
		}
	}

	private static string GetImageContentType(string blobName) =>
		Path.GetExtension(blobName).ToLowerInvariant() switch
		{
			".jpg" or ".jpeg" => "image/jpeg",
			".png" => "image/png",
			".svg" => "image/svg+xml",
			_ => "application/octet-stream"
		};
}
