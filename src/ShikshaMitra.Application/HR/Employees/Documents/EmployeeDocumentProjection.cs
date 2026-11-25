using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees.Settings;

namespace ShikshaMitra.HR.Employees.Documents;

public static class EmployeeDocumentProjection
{
	public static IQueryable<EmployeeDocumentResponseDto> QueryList(
		IQueryable<EmployeeDocumentReadItem> documents, IQueryable<DocumentSetting> definitions, string? search)
	{
		var query = from document in documents
					join definition in definitions on document.DocumentNameId equals (Guid?)definition.Id into documentDefinitions
					from definition in documentDefinitions.DefaultIfEmpty()
					select new EmployeeDocumentResponseDto
					{
						DocumentId = document.Id,
						DocumentNameId = document.DocumentNameId,
						DocumentName = definition == null ? null : definition.DocumentName,
						FileName = document.DocumentName,
						ContentType = document.ContentType,
						Size = document.Size,
						UploadedAt = document.CreationTime,
						UpdatedAt = document.LastModificationTime
					};

		if (string.IsNullOrWhiteSpace(search)) return query;

		var term = search.ToLowerInvariant();
		return query.Where(document => document.FileName.ToLower().Contains(term)
			|| (document.DocumentName != null && document.DocumentName.ToLower().Contains(term)));
	}

	public static EmployeeDocumentResponseDto ToListItem(EmployeeDocumentReadItem document, IReadOnlyDictionary<Guid, string> settingsLookup) => new()
	{
		DocumentId = document.Id,
		DocumentNameId = document.DocumentNameId,
		DocumentName = document.DocumentNameId.HasValue && settingsLookup.TryGetValue(document.DocumentNameId.Value, out var settingName)
			? settingName : null,
		FileName = document.DocumentName,
		ContentType = document.ContentType,
		Size = document.Size,
		UploadedAt = document.CreationTime,
		UpdatedAt = document.LastModificationTime
	};

	public static EmployeeDocumentResponseDto ToListItem(EmployeeDocument document, IReadOnlyDictionary<Guid, string> settingsLookup) => new()
	{
		DocumentId = document.Id,
		DocumentNameId = document.DocumentNameId,
		DocumentName = document.DocumentNameId.HasValue && settingsLookup.TryGetValue(document.DocumentNameId.Value, out var settingName)
			? settingName
			: null,
		FileName = document.DocumentName,
		ContentType = document.ContentType,
		Size = document.Size,
		UploadedAt = document.CreationTime,
		UpdatedAt = document.LastModificationTime
	};

	public static DocumentMetadataDto ToMetadata(EmployeeDocument document) => new()
	{
		Id = document.Id,
		FileName = document.DocumentName,
		Size = document.Size,
		ContentType = document.ContentType
	};

	public static DocumentDto ToDocumentDto(EmployeeDocument document, byte[] content) => new()
	{
		FileName = document.DocumentName,
		ContentType = document.ContentType,
		Content = content,
		Size = document.Size
	};

	public static DocumentDto ToUpdatedDocumentDto(EmployeeDocument document, byte[] content) => new()
	{
		Id = document.Id,
		FileName = document.DocumentName,
		ContentType = document.ContentType,
		Content = content,
		Size = document.Size
	};
}
