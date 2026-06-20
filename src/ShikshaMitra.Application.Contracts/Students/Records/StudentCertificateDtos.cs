using System;
using System.Collections.Generic;
using ShikshaMitra.Students.StudentRecords;

namespace ShikshaMitra.Students.Records;

public class StudentCertificatePreviewRequestDto
{
	public Guid StudentId { get; set; }
	public StudentCertificateType CertificateType { get; set; }
	public DateTimeOffset? IssueDate { get; set; }
	public Dictionary<string, string?> CertificateFields { get; set; } = new();
}

public class CreateStudentCertificateDto : StudentCertificatePreviewRequestDto
{
}

public class StudentCertificateResponseDto
{
	public Guid? CertificateId { get; set; }
	public string CertificateNo { get; set; } = string.Empty;
	public string CertificateType { get; set; } = string.Empty;
	public DateTimeOffset IssueDate { get; set; }
	public string Status { get; set; } = string.Empty;
	public StudentCertificateStudentDto Student { get; set; } = new();
	public StudentCertificateOrganizationDto Organization { get; set; } = new();
	public Dictionary<string, string?> CertificateData { get; set; } = new();
	public Dictionary<string, string?> PlaceholderValues { get; set; } = new();
	public StudentCertificateTemplateRenderDto Template { get; set; } = new();
}

public class StudentCertificateStudentDto
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = string.Empty;
	public string? GrNo { get; set; }
	public string? AdmissionNo { get; set; }
	public string? AcademicYearName { get; set; }
	public string? ClassName { get; set; }
	public string? DivisionName { get; set; }
	public DateTimeOffset? DateOfBirth { get; set; }
	public string? Gender { get; set; }
}

public class StudentCertificateOrganizationDto
{
	public Guid? OrganizationId { get; set; }
	public string OrganizationName { get; set; } = string.Empty;
	public string? LogoUrl { get; set; }
	public string? Address { get; set; }
	public string? PhoneNumber { get; set; }
}

public class StudentCertificateTemplateRenderDto
{
	public string HeaderContent { get; set; } = string.Empty;
	public string FooterContent { get; set; } = string.Empty;
	public string RenderedHeaderContent { get; set; } = string.Empty;
	public string RenderedFooterContent { get; set; } = string.Empty;
	public string? PrincipalSignatureUrl { get; set; }
	public string? SchoolStampUrl { get; set; }
	public string? LogoUrl { get; set; }
	public string? SchoolAddress { get; set; }
}

public class StudentCertificateTemplateDto
{
	public Guid? Id { get; set; }
	public StudentCertificateType CertificateType { get; set; }
	public string HeaderContent { get; set; } = string.Empty;
	public string FooterContent { get; set; } = string.Empty;
	public string? LogoUrl { get; set; }
	public string? SchoolAddress { get; set; }
	public string? PrincipalSignatureUrl { get; set; }
	public string? SchoolStampUrl { get; set; }
	public bool IsActive { get; set; } = true;
}

public class StudentCertificateHistoryDto
{
	public Guid CertificateId { get; set; }
	public string CertificateNumber { get; set; } = string.Empty;
	public string CertificateType { get; set; } = string.Empty;
	public DateTimeOffset IssueDate { get; set; }
	public Guid? GeneratedBy { get; set; }
	public string Status { get; set; } = string.Empty;
	public DateTime CreatedDate { get; set; }
	public List<string> SupportedActions { get; set; } = new() { "View", "Download", "Print", "Duplicate" };
}

public class StudentCertificatePdfDto
{
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = "application/pdf";
	public byte[] Content { get; set; } = Array.Empty<byte>();
}
