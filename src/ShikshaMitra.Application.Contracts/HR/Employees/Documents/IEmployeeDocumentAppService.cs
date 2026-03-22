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
	Task UploadDocumentsAsync(Guid employeeId, List<DocumentDto> documents);
	Task<DocumentDto> GetDocumentAsync(Guid employeeId, Guid documentId);
	Task<List<DocumentMetadataDto>> GetDocumentMetadataAsync(Guid employeeId);
	Task<DocumentDto> UpdateDocumentAsync(Guid employeeId, Guid documentId, DocumentDto documentDto);
	Task DeleteDocumentAsync(Guid employeeId, Guid documentId);
	Task<string> UploadProfilePictureAsync(Guid employeeId, string fileName, byte[] fileContent);
	Task<DocumentDto?> GetProfilePictureAsync(Guid employeeId);
}
