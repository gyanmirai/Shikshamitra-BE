using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;

namespace ShikshaMitra.HR.Employees.Documents;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
public class EmployeeDocumentAppService(
	IEmployeeRepository employeeRepository,
	EmployeeDocumentManager employeeDocumentManager,
	IDocumentSettingRepository documentSettingRepository,
	IBlobContainer blobContainer,
	ILogger<EmployeeDocumentAppService> logger)
	: ApplicationService, IEmployeeDocumentAppService
{
	public async Task UploadDocumentsAsync(Guid employeeId, List<DocumentDto> documents)
	{
		var employeeDocuments = documents.ConvertAll(doc => new EmployeeDocumentData
		{
			FileName = doc.FileName,
			ContentType = doc.ContentType,
			Content = doc.Content,
			Size = doc.Size
		});

		await employeeDocumentManager.AddDocumentsAsync(employeeId, employeeDocuments);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public async Task UploadDocumentAsync(Guid employeeId, Guid documentNameId, string fileName, string contentType, byte[] content, long size)
	{
		await EnsureDocumentSettingExistsAsync(documentNameId);

		var documentData = new EmployeeDocumentData
		{
			DocumentNameId = documentNameId,
			FileName = fileName,
			ContentType = contentType,
			Content = content,
			Size = size
		};

		await employeeDocumentManager.AddDocumentsAsync(employeeId, [documentData]);
	}

	public async Task<EmployeeDocumentListResponse> GetDocumentListAsync(Guid employeeId, int page, int pageSize, string? search)
	{
		const int defaultPageSize = 10;
		const int maxPageSize = 100;

		var normalizedPage = page < 1 ? 1 : page;
		var normalizedPageSize = pageSize < 1 ? defaultPageSize : Math.Min(pageSize, maxPageSize);

		var documents = await employeeDocumentManager.GetDocumentsAsync(employeeId);

		// Resolve document setting names for all distinct DocumentNameIds
		var nameIds = documents
			.Where(d => d.DocumentNameId.HasValue)
			.Select(d => d.DocumentNameId!.Value)
			.Distinct()
			.ToList();

		var settingsLookup = new Dictionary<Guid, string>();
		if (nameIds.Count > 0)
		{
			var settings = await documentSettingRepository.GetListAsync(s => nameIds.Contains(s.Id));
			settingsLookup = settings.ToDictionary(s => s.Id, s => s.DocumentName);
		}

		IEnumerable<EmployeeDocument> filtered = documents;
		if (!string.IsNullOrWhiteSpace(search))
		{
			filtered = filtered.Where(d =>
				d.DocumentName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
				(d.DocumentNameId.HasValue && settingsLookup.TryGetValue(d.DocumentNameId.Value, out var name) &&
				 name.Contains(search, StringComparison.OrdinalIgnoreCase)));
		}

		var totalCount = filtered.Count();
		var items = filtered
			.OrderByDescending(d => d.CreationTime)
			.Skip((normalizedPage - 1) * normalizedPageSize)
			.Take(normalizedPageSize)
			.Select(d => new EmployeeDocumentResponseDto
			{
				DocumentId = d.Id,
				DocumentNameId = d.DocumentNameId,
				DocumentName = d.DocumentNameId.HasValue && settingsLookup.TryGetValue(d.DocumentNameId.Value, out var settingName)
					? settingName : null,
				FileName = d.DocumentName,
				ContentType = d.ContentType,
				Size = d.Size,
				UploadedAt = d.CreationTime,
				UpdatedAt = d.LastModificationTime
			})
			.ToList();

		var totalPages = totalCount == 0
			? 0
			: (int)Math.Ceiling((double)totalCount / normalizedPageSize);

		return new EmployeeDocumentListResponse
		{
			TotalCount = totalCount,
			CurrentPage = normalizedPage,
			PageSize = normalizedPageSize,
			TotalPages = totalPages,
			Items = items
		};
	}

	public async Task<DocumentDto> GetDocumentAsync(Guid employeeId, Guid documentId)
	{
		var document = await employeeDocumentManager.GetDocumentAsync(employeeId, documentId);

		await using var stream = await blobContainer.GetAsync(document.BlobName);
		await using var memoryStream = new MemoryStream();
		await stream.CopyToAsync(memoryStream);

		return new DocumentDto
		{
			FileName = document.DocumentName,
			ContentType = document.ContentType,
			Content = memoryStream.ToArray(),
			Size = document.Size
		};
	}

	public async Task<List<DocumentMetadataDto>> GetDocumentMetadataAsync(Guid employeeId)
	{
		var documents = await employeeDocumentManager.GetDocumentsAsync(employeeId);
		return documents.Select(doc => new DocumentMetadataDto
		{
			Id = doc.Id,
			FileName = doc.DocumentName,
			Size = doc.Size,
			ContentType = doc.ContentType
		}).ToList();
	}

	public async Task<DocumentDto> UpdateDocumentAsync(Guid employeeId, Guid documentId, DocumentDto documentDto)
	{
		var updatedDocument = await employeeDocumentManager.UpdateDocumentAsync(
			employeeId,
			documentId,
			documentDto.FileName,
			documentDto.ContentType,
			documentDto.Size,
			documentDto.Content);

		return new DocumentDto
		{
			FileName = updatedDocument.DocumentName,
			ContentType = updatedDocument.ContentType,
			Content = documentDto.Content,
			Size = updatedDocument.Size,
			Id = updatedDocument.Id
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public async Task UpdateDocumentAsync(Guid employeeId, Guid documentId, Guid documentNameId, string fileName, string contentType, byte[] content, long size)
	{
		await EnsureDocumentSettingExistsAsync(documentNameId);

		await employeeDocumentManager.UpdateDocumentAsync(
			employeeId,
			documentId,
			fileName,
			contentType,
			size,
			content,
			documentNameId);
	}

	public async Task DeleteDocumentAsync(Guid employeeId, Guid documentId)
	{
		await employeeDocumentManager.DeleteDocumentAsync(employeeId, documentId);
	}

	public async Task<string> UploadProfilePictureAsync(Guid employeeId, string fileName, byte[] fileContent)
	{
		return await employeeDocumentManager.UploadProfilePictureAsync(employeeId, fileName, fileContent);
	}

	public async Task<DocumentDto?> GetProfilePictureAsync(Guid employeeId)
	{
		var employee = await employeeRepository.GetAsync(employeeId);

		if (string.IsNullOrEmpty(employee.ProfilePictureBlobName))
		{
			return null;
		}

		try
		{
			await using var stream = await blobContainer.GetAsync(employee.ProfilePictureBlobName);
			await using var memoryStream = new MemoryStream();
			await stream.CopyToAsync(memoryStream);

			var contentType = GetContentTypeFromFileName(employee.ProfilePictureBlobName);

			return new DocumentDto
			{
				FileName = employee.ProfilePictureBlobName,
				ContentType = contentType,
				Content = memoryStream.ToArray(),
				Size = memoryStream.Length
			};
		}
		catch (FileNotFoundException)
		{
			logger.LogWarning(
				"Profile picture blob not found for employee {EmployeeId}. BlobName: {BlobName}",
				employeeId,
				employee.ProfilePictureBlobName);
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

	private async Task EnsureDocumentSettingExistsAsync(Guid documentNameId)
	{
		var setting = await documentSettingRepository.FindAsync(documentNameId);
		if (setting == null)
			throw new BusinessException(HRErrorCodes.EmployeeDocumentConfigurationNotFound);
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
