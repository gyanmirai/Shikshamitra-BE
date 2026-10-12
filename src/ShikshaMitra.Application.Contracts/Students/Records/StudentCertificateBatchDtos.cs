using System;
using System.Collections.Generic;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Records;

public class StudentCertificateBatchQuery : PagedResultRequestDto
{
	public DateTimeOffset GeneratedFrom { get; set; }
	public DateTimeOffset GeneratedTo { get; set; }
	public StudentCertificateType CertificateType { get; set; }
}

public class StudentCertificateBatchDto
{
	public Guid Id { get; set; }
	public string CertificateType { get; set; } = "";
	public DateTimeOffset GeneratedAt { get; set; }
	public string FirstCertificateNumber { get; set; } = "";
	public string LastCertificateNumber { get; set; } = "";
	public int StudentCount { get; set; }
	public string Status { get; set; } = "";
}

public class StudentCertificateBatchDetailDto
{
	public Guid Id { get; set; }
	public List<StudentCertificateResponseDto> Certificates { get; set; } = [];
}
