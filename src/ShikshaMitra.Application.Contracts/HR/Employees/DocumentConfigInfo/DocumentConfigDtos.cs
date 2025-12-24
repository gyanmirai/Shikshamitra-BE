using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Employees.DocumentConfigInfo;

/// <summary>
/// DTO for document configuration item
/// </summary>
public class DocumentConfigDto
{
	public Guid Id { get; set; }
	public string Title { get; set; } = string.Empty;
	public int DocumentSize { get; set; }
	public List<int> DocumentTypes { get; set; } = new();
}

/// <summary>
/// DTO for creating document configuration
/// </summary>
public class CreateDocumentConfigDto
{
	public string Title { get; set; } = string.Empty;
	public int DocumentSize { get; set; }
	public List<int> DocumentTypes { get; set; } = new();
}

/// <summary>
/// DTO for updating document configuration
/// </summary>
public class UpdateDocumentConfigDto
{
	public string Title { get; set; } = string.Empty;
	public int DocumentSize { get; set; }
	public List<int> DocumentTypes { get; set; } = new();
}
