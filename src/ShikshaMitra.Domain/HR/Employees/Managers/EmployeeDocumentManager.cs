using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Domain service for managing employee documents and profile pictures.
/// Owns all blob storage interactions related to employees.
/// </summary>
public class EmployeeDocumentManager(
	IEmployeeRepository employeeRepository,
	IBlobContainer blobContainer
) : DomainService
{
	public async Task AddDocumentsAsync(Guid employeeId, List<EmployeeDocumentData> documents)
	{
		var employee = await employeeRepository.GetAsync(employeeId);

		foreach (var docData in documents)
		{
			FileValidationService.ValidateDocumentUpload(docData.FileName, docData.Content, docData.Size);

			var documentId = GuidGenerator.Create();

			// Sanitize filename to prevent path traversal
			var sanitizedFileName = Path.GetFileName(docData.FileName);
			var blobName = $"employee-documents/{employeeId}/{documentId}_{sanitizedFileName}";

			await blobContainer.SaveAsync(blobName, docData.Content, overrideExisting: true);

			var document = new EmployeeDocument(
				id: documentId,
				tenantId: CurrentTenant.Id,
				employeeId: employeeId,
				documentName: docData.FileName,
				contentType: docData.ContentType,
				size: docData.Size,
				blobName: blobName,
				documentNameId: docData.DocumentNameId
			);

			employee.AddDocument(document);
		}

		await employeeRepository.UpdateAsync(employee);
	}

	public async Task<EmployeeDocument> GetDocumentAsync(Guid employeeId, Guid documentId)
	{
		var employee = await employeeRepository.GetAsync(employeeId);
		var document = employee.Documents.FirstOrDefault(d => d.Id == documentId);
		return document
			?? throw new BusinessException(HRErrorCodes.EmployeeDocumentNotFound);
	}

	public async Task<List<EmployeeDocument>> GetDocumentsAsync(Guid employeeId)
	{
		var employee = await employeeRepository.GetAsync(employeeId);
		return employee.Documents.ToList();
	}

	public async Task<EmployeeDocument> UpdateDocumentAsync(
		Guid employeeId,
		Guid documentId,
		string fileName,
		string contentType,
		long size,
		byte[] content,
		Guid? documentNameId = null
	)
	{
		FileValidationService.ValidateDocumentUpload(fileName, content, size);

		var employee = await employeeRepository.GetAsync(employeeId);
		var document = employee.Documents.FirstOrDefault(d => d.Id == documentId)
			?? throw new BusinessException(HRErrorCodes.EmployeeDocumentNotFound);

		await blobContainer.SaveAsync(document.BlobName, content, overrideExisting: true);

		document.UpdateDocumentMetadata(fileName, contentType, size, documentNameId);

		await employeeRepository.UpdateAsync(employee);
		return document;
	}

	public async Task DeleteDocumentAsync(Guid employeeId, Guid documentId)
	{
		var employee = await employeeRepository.GetAsync(employeeId);
		var document = employee.Documents.FirstOrDefault(d => d.Id == documentId)
			?? throw new BusinessException(HRErrorCodes.EmployeeDocumentNotFound);

		await blobContainer.DeleteAsync(document.BlobName);

		employee.Documents.Remove(document);

		await employeeRepository.UpdateAsync(employee);
	}

	public async Task<string> UploadProfilePictureAsync(Guid employeeId, string fileName, byte[] fileContent)
	{
		FileValidationService.ValidateProfilePictureUpload(fileName, fileContent);

		// SECURITY: Sanitize filename to prevent path traversal attacks
		var sanitizedFileName = Path.GetFileName(fileName);

		var employee = await employeeRepository.GetAsync(employeeId);

		// Delete old profile picture if it exists
		if (!string.IsNullOrEmpty(employee.ProfilePictureBlobName))
		{
			await blobContainer.DeleteAsync(employee.ProfilePictureBlobName);
		}

		var blobName = $"profile-pictures/{employeeId}_{Guid.NewGuid()}_{sanitizedFileName}";

		await blobContainer.SaveAsync(blobName, fileContent, overrideExisting: true);

		employee.UpdateProfilePicture(blobName);
		await employeeRepository.UpdateAsync(employee);

		return blobName;
	}
}
