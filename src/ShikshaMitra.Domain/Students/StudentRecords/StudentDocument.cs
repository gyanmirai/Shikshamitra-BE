using System;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentCertificate : AggregateRoot<Guid>, IMultiTenant
{
	public Guid StudentId { get; set; }
	public byte[] PdfContent { get; set; }
	public string FileName { get; set; }
	public string? Reason { get; set; }

	public DateTimeOffset CreationTime { get; set; }


	public Student Student { get; set; } // Navigation property

	public Guid? TenantId { get; }

	// Parameterless constructor required by EF Core and ABP.
	private StudentCertificate()
	{
	}

	public StudentCertificate(Guid id, Guid tenantId, Guid studentId, byte[] pdfContent, string fileName, DateTimeOffset creationTime, string? reason = null)
		: base(id)
	{
		StudentId = studentId;
		TenantId = tenantId;
		PdfContent = pdfContent;
		FileName = fileName;
		CreationTime = creationTime;
		Reason = reason;
	}
}
