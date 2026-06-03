using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Common;

namespace ShikshaMitra.HR.Employees.Settings;

internal static class DocumentSettingProjection
{
	public static DocumentListResponse ToListResponse(
		IReadOnlyCollection<DocumentSetting> documents,
		int totalCount,
		int page,
		int pageSize)
	{
		return new DocumentListResponse
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
			Items = documents.Select(ToItem).ToList()
		};
	}

	public static DocumentItem ToItem(DocumentSetting document)
	{
		return new DocumentItem
		{
			Id = document.Id,
			Title = document.DocumentName,
			DocumentSize = (int)document.DocumentSize,
			DocumentTypes = document.DocumentTypes.Select(documentType => (int)documentType).ToList()
		};
	}

	public static DocumentCreateResponse ToCreateResponse(DocumentSetting document)
	{
		return new DocumentCreateResponse
		{
			Id = document.Id
		};
	}

	public static List<DocumentType> ToDocumentTypes(IEnumerable<int> documentTypes)
	{
		return documentTypes
			.Select(documentType => (DocumentType)documentType)
			.ToList();
	}
}
