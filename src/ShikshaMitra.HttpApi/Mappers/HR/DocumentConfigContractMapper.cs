using System;
using System.Linq;

namespace ShikshaMitra.HR.Employees.DocumentConfigInfo;

/// <summary>
/// Maps between API contract DTOs and internal application DTOs
/// </summary>
public static class DocumentConfigContractMapper
{
	public static CreateDocumentConfigDto ToCreateDto(DocumentRequest request)
	{
		return new CreateDocumentConfigDto
		{
			Title = request.Title,
			DocumentSize = request.DocumentSize,
			DocumentTypes = request.DocumentTypes
		};
	}

	public static UpdateDocumentConfigDto ToUpdateDto(DocumentRequest request)
	{
		return new UpdateDocumentConfigDto
		{
			Title = request.Title,
			DocumentSize = request.DocumentSize,
			DocumentTypes = request.DocumentTypes
		};
	}

	public static DocumentItemResponse ToItemResponse(DocumentConfigDto dto)
	{
		return new DocumentItemResponse
		{
			Id = dto.Id,
			Title = dto.Title,
			DocumentSize = dto.DocumentSize,
			DocumentTypes = dto.DocumentTypes.ToList()
		};
	}

	public static DocumentListResponse ToListResponse(System.Collections.Generic.IEnumerable<DocumentConfigDto> dtos)
	{
		return new DocumentListResponse
		{
			Items = dtos.Select(ToItemResponse).ToList()
		};
	}
}
