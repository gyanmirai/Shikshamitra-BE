using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentCertificateDto : EntityDto<Guid>
{
	public int StudentId { get; set; }
	public byte[] PdfContent { get; set; }
	public string FileName { get; set; }
	public string? Reason { get; set; }
	public DateTimeOffset CreationTime { get; set; }
}
