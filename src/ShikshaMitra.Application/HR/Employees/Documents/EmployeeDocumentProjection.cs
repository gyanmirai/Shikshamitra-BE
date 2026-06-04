using System;
using System.Collections.Generic;
using ShikshaMitra.Common;

namespace ShikshaMitra.HR.Employees.Documents;

public static class EmployeeDocumentProjection
{
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
