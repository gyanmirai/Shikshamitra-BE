using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.Documents;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
public class EmployeeDocumentAppService(
	EmployeeDocumentManager employeeDocumentManager,
	EmployeeDocumentConfigurationValidator documentConfigurationValidator,
	EmployeeDocumentReadService documentReadService,
	EmployeeProfilePictureWorkflow profilePictureWorkflow)
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
		await documentConfigurationValidator.EnsureExistsAsync(documentNameId);

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

	public Task<EmployeeDocumentListResponse> GetDocumentListAsync(Guid employeeId, int page, int pageSize, string? search) =>
		documentReadService.GetDocumentListAsync(employeeId, page, pageSize, search);

	public Task<DocumentDto> GetDocumentAsync(Guid employeeId, Guid documentId) =>
		documentReadService.GetDocumentAsync(employeeId, documentId);

	public Task<List<DocumentMetadataDto>> GetDocumentMetadataAsync(Guid employeeId) =>
		documentReadService.GetDocumentMetadataAsync(employeeId);

	public async Task<DocumentDto> UpdateDocumentAsync(Guid employeeId, Guid documentId, DocumentDto documentDto)
	{
		var updatedDocument = await employeeDocumentManager.UpdateDocumentAsync(
			employeeId,
			documentId,
			documentDto.FileName,
			documentDto.ContentType,
			documentDto.Size,
			documentDto.Content);

		return EmployeeDocumentProjection.ToUpdatedDocumentDto(updatedDocument, documentDto.Content);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public async Task UpdateDocumentAsync(Guid employeeId, Guid documentId, Guid documentNameId, string fileName, string contentType, byte[] content, long size)
	{
		await documentConfigurationValidator.EnsureExistsAsync(documentNameId);

		await employeeDocumentManager.UpdateDocumentAsync(
			employeeId,
			documentId,
			fileName,
			contentType,
			size,
			content,
			documentNameId);
	}

	public Task DeleteDocumentAsync(Guid employeeId, Guid documentId) =>
		employeeDocumentManager.DeleteDocumentAsync(employeeId, documentId);

	public Task<string> UploadProfilePictureAsync(Guid employeeId, string fileName, byte[] fileContent) =>
		profilePictureWorkflow.UploadProfilePictureAsync(employeeId, fileName, fileContent);

	public Task<DocumentDto?> GetProfilePictureAsync(Guid employeeId) =>
		profilePictureWorkflow.GetProfilePictureAsync(employeeId);
}
