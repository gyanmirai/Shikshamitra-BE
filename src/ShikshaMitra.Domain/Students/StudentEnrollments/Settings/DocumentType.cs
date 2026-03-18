using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

/// <summary>
/// Represents a type of document that can be uploaded (e.g., Birth Certificate, PAN Card)
/// This is a tenant-specific configuration entity
/// </summary>
public class DocumentType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	/// <summary>
	/// Tenant ID for multi-tenancy support
	/// </summary>
	public Guid? TenantId { get; private set; }

	/// <summary>
	/// Name of the document type (e.g., "Birth Certificate", "PAN Card")
	/// </summary>
	public string Name { get; private set; }

	/// <summary>
	/// Maximum allowed file size in MB
	/// </summary>
	public double MaxSizeMB { get; private set; }

	/// <summary>
	/// Comma-separated list of accepted file format enum values
	/// Stored as string for EF Core compatibility (e.g., "1,2,3" for Jpg,Jpeg,Png)
	/// </summary>
	public string AcceptedFormatsData { get; private set; }

	/// <summary>
	/// Whether this document type is required for student enrollment
	/// </summary>
	public bool IsRequired { get; private set; }

	/// <summary>
	/// Display order for sorting
	/// </summary>
	public int DisplayOrder { get; private set; }

	/// <summary>
	/// Gets the accepted formats as a list of enums
	/// </summary>
	public List<AcceptedFileFormat> GetAcceptedFormats()
	{
		if (string.IsNullOrWhiteSpace(AcceptedFormatsData))
		{
			return new List<AcceptedFileFormat>();
		}

		return AcceptedFormatsData
			.Split(',', StringSplitOptions.RemoveEmptyEntries)
			.Select(x => (AcceptedFileFormat)int.Parse(x))
			.ToList();
	}

	/// <summary>
	/// Sets the accepted formats from a list of enums
	/// </summary>
	private void SetAcceptedFormats(List<AcceptedFileFormat> formats)
	{
		if (formats == null || formats.Count == 0)
		{
			throw new ArgumentException("At least one file format must be accepted", nameof(formats));
		}

		// Remove duplicates and sort
		var uniqueFormats = formats.Distinct().OrderBy(x => x).ToList();

		// Store as comma-separated enum values
		AcceptedFormatsData = string.Join(",", uniqueFormats.Select(x => (int)x));
	}

	/// <summary>
	/// ORM constructor
	/// </summary>
	private DocumentType()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	/// <summary>
	/// Creates a new document type
	/// </summary>
	internal DocumentType(
		Guid id,
		Guid? tenantId,
		string name,
		double maxSizeMB,
		List<AcceptedFileFormat> acceptedFormats,
		bool isRequired = false,
		int displayOrder = 0)
		: base(id)
	{
		TenantId = tenantId;
		SetName(name);
		SetMaxSizeMB(maxSizeMB);
		SetAcceptedFormats(acceptedFormats);
		IsRequired = isRequired;
		DisplayOrder = displayOrder;
	}

	/// <summary>
	/// Updates the document type details
	/// </summary>
	public DocumentType Update(
		string name,
		double maxSizeMB,
		List<AcceptedFileFormat> acceptedFormats,
		bool isRequired,
		int displayOrder)
	{
		SetName(name);
		SetMaxSizeMB(maxSizeMB);
		SetAcceptedFormats(acceptedFormats);
		IsRequired = isRequired;
		DisplayOrder = displayOrder;
		return this;
	}

	/// <summary>
	/// Sets the document type name with validation
	/// </summary>
	private DocumentType SetName(string name)
	{
		Name = Check.NotNullOrWhiteSpace(
			name,
			nameof(name),
			maxLength: DocumentTypeConsts.MaxNameLength
		);
		return this;
	}

	/// <summary>
	/// Sets the max size with validation
	/// </summary>
	private DocumentType SetMaxSizeMB(double maxSizeMB)
	{
		if (maxSizeMB < DocumentTypeConsts.MinDocumentSizeMB)
		{
			throw new ArgumentException(
				$"Max size must be at least {DocumentTypeConsts.MinDocumentSizeMB} MB",
				nameof(maxSizeMB));
		}

		if (maxSizeMB > DocumentTypeConsts.MaxDocumentSizeMB)
		{
			throw new ArgumentException(
				$"Max size cannot exceed {DocumentTypeConsts.MaxDocumentSizeMB} MB",
				nameof(maxSizeMB));
		}

		MaxSizeMB = maxSizeMB;
		return this;
	}

	/// <summary>
	/// Updates display order
	/// </summary>
	public void UpdateDisplayOrder(int displayOrder)
	{
		DisplayOrder = displayOrder;
	}
}
