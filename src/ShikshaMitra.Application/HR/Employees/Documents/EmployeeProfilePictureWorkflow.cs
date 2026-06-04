using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp;

namespace ShikshaMitra.HR.Employees.Documents;

[RemoteService(false)]
public class EmployeeProfilePictureWorkflow(
	IEmployeeRepository employeeRepository,
	EmployeeDocumentManager employeeDocumentManager,
	EmployeeDocumentReadService documentReadService,
	ILogger<EmployeeProfilePictureWorkflow> logger)
	: ApplicationService, ITransientDependency
{
	public Task<string> UploadProfilePictureAsync(Guid employeeId, string fileName, byte[] fileContent) =>
		employeeDocumentManager.UploadProfilePictureAsync(employeeId, fileName, fileContent);

	public async Task<DocumentDto?> GetProfilePictureAsync(Guid employeeId)
	{
		var employee = await employeeRepository.GetAsync(employeeId);

		if (string.IsNullOrEmpty(employee.ProfilePictureBlobName))
		{
			return null;
		}

		try
		{
			var content = await documentReadService.ReadBlobAsync(employee.ProfilePictureBlobName);

			return new DocumentDto
			{
				FileName = employee.ProfilePictureBlobName,
				ContentType = GetContentTypeFromFileName(employee.ProfilePictureBlobName),
				Content = content,
				Size = content.LongLength
			};
		}
		catch (FileNotFoundException)
		{
			LogMissingProfilePicture(employeeId, employee.ProfilePictureBlobName);
			return null;
		}
		catch (Exception ex) when (ex.GetType().Name == "BlobProviderNotFoundException"
			|| ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
		{
			logger.LogWarning(
				"Profile picture blob not found for employee {EmployeeId}. BlobName: {BlobName}. Exception: {ExceptionType}",
				employeeId,
				employee.ProfilePictureBlobName,
				ex.GetType().Name);
			return null;
		}
	}

	private void LogMissingProfilePicture(Guid employeeId, string blobName)
	{
		logger.LogWarning(
			"Profile picture blob not found for employee {EmployeeId}. BlobName: {BlobName}",
			employeeId,
			blobName);
	}

	private static string GetContentTypeFromFileName(string fileName)
	{
		var extension = Path.GetExtension(fileName)?.ToLowerInvariant();

		return extension switch
		{
			".jpg" or ".jpeg" => "image/jpeg",
			".png" => "image/png",
			".gif" => "image/gif",
			".bmp" => "image/bmp",
			".webp" => "image/webp",
			".svg" => "image/svg+xml",
			".ico" => "image/x-icon",
			_ => "application/octet-stream"
		};
	}
}
