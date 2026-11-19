using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Employees.Documents;

public class EmployeeDocumentChecklistItemDto
{
	public Guid Id { get; set; }
	public Guid EmployeeId { get; set; }
	public string Title { get; set; } = "";
	public float DocumentSize { get; set; }
	public List<int> DocumentTypes { get; set; } = [];
	public string DocType { get; set; } = "";
	public string StatusVal { get; set; } = "Not Uploaded";
	public EmployeeDocumentResponseDto? UploadedDoc { get; set; }
}

public class EmployeeDocumentResponseDto
{
	public Guid DocumentId { get; set; }
	public Guid? DocumentNameId { get; set; }
	public string? DocumentName { get; set; }
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public long Size { get; set; }
	public DateTime UploadedAt { get; set; }
	public DateTime? UpdatedAt { get; set; }
}

public class EmployeeDocumentListResponse
{
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public List<EmployeeDocumentResponseDto> Items { get; set; } = new();
}
