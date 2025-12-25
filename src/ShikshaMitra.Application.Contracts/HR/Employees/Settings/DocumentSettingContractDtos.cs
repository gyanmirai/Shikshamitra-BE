using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Employees.Settings;

/// <summary>
/// Request for create/update document configuration (matches OpenAPI DocumentRequest)
/// </summary>
public class DocumentRequest
{
	/// <summary>
	/// Document name (maps to DocumentName in entity)
	/// Pattern: ^[A-Za-z ]+$ (alphabets and spaces only)
	/// </summary>
	[Required]
	[RegularExpression(@"^[A-Za-z ]+$", ErrorMessage = "Title must contain only alphabets and spaces")]
	public string Title { get; set; } = string.Empty;
	
	/// <summary>
	/// Maximum file size in MB (1-20)
	/// </summary>
	[Required]
	[Range(1, 20, ErrorMessage = "Document size must be between 1 and 20 MB")]
	public int DocumentSize { get; set; }
	
	/// <summary>
	/// Accepted document formats as integer enum values (0-5)
	/// 0=Jpg, 1=Jpeg, 2=Png, 3=Docx, 4=Pdf, 5=Xlsx
	/// </summary>
	[Required]
	[MinLength(1, ErrorMessage = "At least one document type must be selected")]
	public List<int> DocumentTypes { get; set; } = new();
}

/// <summary>
/// Document item response (matches OpenAPI DocumentItem)
/// </summary>
public class DocumentItem
{
	public Guid Id { get; set; }
	
	/// <summary>
	/// Document title (maps from DocumentName)
	/// </summary>
	public string Title { get; set; } = string.Empty;
	
	/// <summary>
	/// Maximum file size in MB
	/// </summary>
	public int DocumentSize { get; set; }
	
	/// <summary>
	/// Accepted document formats as integer enum values
	/// </summary>
	public List<int> DocumentTypes { get; set; } = new();
}

/// <summary>
/// Response for document list
/// </summary>
public class DocumentListResponse
{
	public List<DocumentItem> Items { get; set; } = new();
}

/// <summary>
/// Response for create document (returns ID)
/// </summary>
public class DocumentCreateResponse
{
	public Guid Id { get; set; }
}

