using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace ShikshaMitra.HR.Employees.DocumentConfigInfo;

/// <summary>
/// Request DTO for creating/updating document configuration
/// </summary>
public class DocumentRequest
{
	public string Title { get; set; } = string.Empty;

	public int DocumentSize { get; set; }

	public List<int> DocumentTypes { get; set; } = new();
}

public class DocumentRequestValidator : AbstractValidator<DocumentRequest>
{
	public DocumentRequestValidator()
	{
		RuleFor(x => x.Title).NotEmpty().Matches("^[A-Za-z ]+$");
		RuleFor(x => x.DocumentSize).InclusiveBetween(1, 20);
		RuleFor(x => x.DocumentTypes).NotNull().NotEmpty();
	}
}

/// <summary>
/// Response DTO for document item
/// </summary>
public class DocumentItemResponse
{
	[Required]
	public Guid Id { get; set; }

	[Required]
	public string Title { get; set; } = string.Empty;

	[Required]
	public int DocumentSize { get; set; }

	[Required]
	public List<int> DocumentTypes { get; set; } = new();
}

/// <summary>
/// Response DTO for paginated document list
/// </summary>
public class DocumentListResponse
{
	[Required]
	public List<DocumentItemResponse> Items { get; set; } = new();
}

/// <summary>
/// Response DTO for create operation
/// </summary>
public class DocumentCreateResponse
{
	[Required]
	public Guid Id { get; set; }
}
