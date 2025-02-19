using System;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentCertificate : AggregateRoot<Guid>, IMultiTenant
{
	public int StudentId { get; set; }
	public byte[] PdfContent { get; set; }
	public string FileName { get; set; }
	public string? Reason { get; set; }

	public DateTimeOffset CreationTime { get; set; }

	public Guid? TenantId { get; }

	// Parameterless constructor required by EF Core and ABP.
	private StudentCertificate()
	{
	}

	public StudentCertificate(Guid id, Guid tenantId, int studentId, byte[] pdfContent, string fileName, DateTimeOffset creationTime, string? reason = null)
		: base(id)
	{
		StudentId = studentId;
		PdfContent = pdfContent;
		FileName = fileName;
		CreationTime = creationTime;
		Reason = reason;
	}
}
