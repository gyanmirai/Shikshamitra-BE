using ShikshaMitra.Common;
using ShikshaMitra.Constants;
using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Employees.Settings;

public class DocumentSetting : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string DocumentName { get; private set; } = string.Empty;
	public float DocumentSize { get; private set; } = AppConstants.DefaultFileSize;
	public bool IsRequired { get; private set; }
	public List<DocumentType> DocumentTypes { get; private set; } = new();

	// For EF Core
	protected DocumentSetting()
	{
	}

	public DocumentSetting(
		Guid id,
		Guid? tenantId,
		string documentName,
		float documentSize,
		bool isRequired,
		List<DocumentType> documentTypes) : base(id)
	{
		TenantId = tenantId;
		DocumentName = documentName;
		DocumentSize = documentSize;
		IsRequired = isRequired;
		DocumentTypes = documentTypes ?? new List<DocumentType>();
	}

	public void Update(
		string documentName,
		float documentSize,
		bool isRequired,
		List<DocumentType> documentTypes)
	{
		DocumentName = documentName;
		DocumentSize = documentSize;
		IsRequired = isRequired;
		DocumentTypes = documentTypes ?? new List<DocumentType>();
	}
}
