using System;
using Volo.Abp.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Employees;

public class EmployeeDocument : IMultiTenant, IHasCreationTime
{
	public Guid Id { get; private set; }
	public Guid? TenantId { get; private set; }
	public Guid EmployeeId { get; private set; }
	public Guid? DocumentNameId { get; private set; }
	public string DocumentName { get; private set; }
	public string ContentType { get; private set; }
	public long Size { get; private set; }
	public DateTime CreationTime { get; private set; }
	public DateTime? LastModificationTime { get; private set; }

	// Stores the Blob name if using Blob Storage
	public string BlobName { get; private set; }

	private EmployeeDocument() { } // For ORM

	public EmployeeDocument(
		Guid id,
		Guid? tenantId,
		Guid employeeId,
		string documentName,
		string contentType,
		long size,
		string blobName,
		Guid? documentNameId = null)
	{
		Id = id;
		TenantId = tenantId;
		EmployeeId = employeeId;
		DocumentName = documentName;
		ContentType = contentType;
		Size = size;
		BlobName = blobName;
		DocumentNameId = documentNameId;
		CreationTime = DateTime.Now;
	}

	public EmployeeDocument UpdateDocumentMetadata(string documentName, string contentType, long size, Guid? documentNameId = null)
	{
		DocumentName = documentName;
		ContentType = contentType;
		Size = size;
		DocumentNameId = documentNameId ?? DocumentNameId;
		LastModificationTime = DateTime.Now;
		return this;
	}
}