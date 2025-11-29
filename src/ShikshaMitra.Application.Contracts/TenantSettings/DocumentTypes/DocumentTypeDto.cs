using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.DocumentTypes;

/// <summary>
/// DTO for displaying document type information
/// </summary>
public class DocumentTypeDto : FullAuditedEntityDto<Guid>
{
	/// <summary>
	/// Name of the document type
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Maximum allowed file size in MB
	/// </summary>
	public double MaxSizeMB { get; set; }

	/// <summary>
	/// List of accepted file formats
	/// </summary>
	public List<AcceptedFileFormat> AcceptedFormats { get; set; } = new();

	/// <summary>
	/// Whether this document is required
	/// </summary>
	public bool IsRequired { get; set; }

	/// <summary>
	/// Display order
	/// </summary>
	public int DisplayOrder { get; set; }
}
