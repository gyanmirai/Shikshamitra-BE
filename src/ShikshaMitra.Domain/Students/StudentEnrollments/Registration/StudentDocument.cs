using System;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentDocument : Entity<Guid>, IMultiTenant, IHasCreationTime                              
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public string DocumentName { get; private set; }
	public string ContentType { get; private set; }
	public long Size { get; private set; }
	public DateTime CreationTime { get; private set; }

	// Stores the Blob name if using Blob Storage
	public string BlobName { get; private set; }
	
	
	// For ORM
	private StudentDocument(){}
	
	public StudentDocument(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		string documentName,
		string contentType,
		long size,
		string blobName)
	{
		Id = id;
		TenantId = tenantId;
		StudentId = studentId;
		DocumentName = documentName;
		ContentType = contentType;
		Size = size;
		BlobName = blobName;
		CreationTime = DateTime.Now;
	}

	public StudentDocument UpdateStudentDocumentMetadata(string documentName, string contentType, long size)
	{
		DocumentName = documentName;
		ContentType = contentType;
		Size = size;
		return this;
	}
}