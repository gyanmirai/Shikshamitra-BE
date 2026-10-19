using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.Documents;

/// <summary>
/// Application service for managing employee documents and profile pictures.
/// </summary>
public interface IEmployeeDocumentAppService : IApplicationService
{
	Task<Volo.Abp.Application.Dtos.PagedResultDto<EmployeeDocumentChecklistItemDto>> GetChecklistAsync(Guid employeeId, int page = 1, int pageSize = 20, string? search = null);
	Task UploadDocumentsAsync(Guid employeeId, List<DocumentDto> documents);
	Task UploadDocumentAsync(Guid employeeId, Guid documentNameId, string fileName, string contentType, byte[] content, long size);
	Task<EmployeeDocumentListResponse> GetDocumentListAsync(Guid employeeId, int page, int pageSize, string? search);
	Task<DocumentDto> GetDocumentAsync(Guid employeeId, Guid documentId);
	Task<List<DocumentMetadataDto>> GetDocumentMetadataAsync(Guid employeeId);
	Task<DocumentDto> UpdateDocumentAsync(Guid employeeId, Guid documentId, DocumentDto documentDto);
	Task UpdateDocumentAsync(Guid employeeId, Guid documentId, Guid documentNameId, string fileName, string contentType, byte[] content, long size);
	Task DeleteDocumentAsync(Guid employeeId, Guid documentId);
	Task<string> UploadProfilePictureAsync(Guid employeeId, string fileName, byte[] fileContent);
	Task<DocumentDto?> GetProfilePictureAsync(Guid employeeId);
}
