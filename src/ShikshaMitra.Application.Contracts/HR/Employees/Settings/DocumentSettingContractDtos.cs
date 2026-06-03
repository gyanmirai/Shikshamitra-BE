using System;
using System.Collections.Generic;
using FluentValidation;

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
	public string Title { get; set; } = string.Empty;

	/// <summary>
	/// Maximum file size in MB (1-20)
	/// </summary>
	public int DocumentSize { get; set; }

	/// <summary>
	/// Accepted document formats as integer enum values (0-5)
	/// 0=Jpg, 1=Jpeg, 2=Png, 3=Docx, 4=Pdf, 5=Xlsx
	/// </summary>
	public List<int> DocumentTypes { get; set; } = new();
}

public class DocumentRequestValidator : AbstractValidator<DocumentRequest>
{
	public DocumentRequestValidator()
	{
		RuleFor(x => x.Title)
			.NotEmpty()
			.Matches(@"^[A-Za-z ]+$")
			.WithMessage("Title must contain only alphabets and spaces");
		RuleFor(x => x.DocumentSize)
			.InclusiveBetween(1, 20)
			.WithMessage("Document size must be between 1 and 20 MB");
		RuleFor(x => x.DocumentTypes)
			.NotNull()
			.NotEmpty()
			.WithMessage("At least one document type must be selected");
	}
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
/// Response for document list with pagination
/// </summary>
public class DocumentListResponse
{
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public List<DocumentItem> Items { get; set; } = new();
}

/// <summary>
/// Response for create document (returns ID)
/// </summary>
public class DocumentCreateResponse
{
	public Guid Id { get; set; }
}

